using Abstrict.Api.Common;
using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class DisputeService(AppDbContext db, BookingWorkflow workflow, IFileStorage storage, TimeProvider clock) : IDisputeService
{
    private static readonly TimeSpan ResponseWindow = TimeSpan.FromHours(48);
    private static readonly Dictionary<DisputeCategory, (string Label, string Description)> CategoryCatalog = new()
    {
        [DisputeCategory.ServiceQuality] = ("Chất lượng dịch vụ", "Công việc chưa đúng mô tả, checklist hoặc thỏa thuận."),
        [DisputeCategory.Payment] = ("Thanh toán", "Khoản tiền, phụ phí hoặc hoàn tiền có vấn đề."),
        [DisputeCategory.Safety] = ("An toàn", "Sự việc ảnh hưởng đến an toàn của khách hoặc người làm."),
        [DisputeCategory.Damage] = ("Thiệt hại tài sản", "Tài sản bị hư hỏng trong quá trình cung cấp dịch vụ."),
        [DisputeCategory.Loss] = ("Mất tài sản", "Tài sản bị thất lạc hoặc nghi bị lấy mất."),
        [DisputeCategory.Other] = ("Khác", "Vấn đề khác cần admin xem xét.")
    };

    public IReadOnlyCollection<DisputeCategoryResponse> GetCategories() => Enum.GetValues<DisputeCategory>()
        .Select(category => new DisputeCategoryResponse(category, category.ToString(), CategoryCatalog[category].Label, CategoryCatalog[category].Description)).ToArray();

    public async Task<DisputeResponse> OpenAsync(Guid customerUserId, Guid bookingId, OpenDisputeRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Category) || !CategoryCatalog.ContainsKey(request.Category))
            throw Error(400, "INVALID_DISPUTE_CATEGORY", "Loại khiếu nại không hợp lệ.");

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var booking = await workflow.LockAsync(bookingId, cancellationToken);
        if (booking is null || booking.CustomerUserId != customerUserId)
            throw Error(404, "BOOKING_NOT_FOUND", "Không tìm thấy đơn đặt dịch vụ.");
        if (booking.Status is not (BookingStatus.PendingAcceptance or BookingStatus.Completed))
            throw Error(409, "BOOKING_NOT_DISPUTABLE", "Chỉ có thể khiếu nại đơn đã hoàn tất hoặc đang chờ nghiệm thu.");
        if (await db.Disputes.AnyAsync(x => x.BookingId == bookingId && x.Status != DisputeStatus.Closed && x.Status != DisputeStatus.Decided, cancellationToken))
            throw Error(409, "DISPUTE_ALREADY_OPEN", "Đơn này đã có khiếu nại đang được xử lý.");

        var dispute = new Dispute
        {
            BookingId = booking.Id,
            OpenedByUserId = customerUserId,
            Category = request.Category,
            Status = DisputeStatus.Open,
            Description = request.Description.Trim(),
            ResponseDeadlineUtc = now.Add(ResponseWindow),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Disputes.Add(dispute);
        workflow.Transition(booking, BookingStatus.Disputed, customerUserId, BookingTimelineReasons.CustomerDisputeOpened, now);
        workflow.Notify(booking.CustomerUserId, "dispute.opened", "Đã tiếp nhận khiếu nại", $"Khiếu nại cho đơn {booking.BookingNumber} đã được gửi đến admin.", booking, now);
        var adminIds = await db.Users.AsNoTracking().Where(x => x.Role == UserRole.Admin && x.Status == AccountStatus.Active).Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var adminId in adminIds)
            workflow.Notify(adminId, "dispute.opened", "Có khiếu nại mới", $"Đơn {booking.BookingNumber} có khiếu nại mới cần xem xét.", booking, now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadAsync(dispute.Id, false, customerUserId, cancellationToken);
    }

    public async Task<DisputeEvidenceResponse> UploadEvidenceAsync(Guid customerUserId, Guid disputeId, string? description, long length, Stream content, CancellationToken cancellationToken)
    {
        if (length <= 0) throw Error(400, "EMPTY_FILE", "Tệp tải lên đang trống.");
        if (length > KycImage.MaximumBytes) throw Error(413, "FILE_TOO_LARGE", "Ảnh không được vượt quá 5 MB.");
        var header = new byte[8];
        var read = await content.ReadAtLeastAsync(header, 8, false, cancellationToken);
        var image = KycImage.Detect(header.AsSpan(0, read)) ?? throw Error(415, "UNSUPPORTED_IMAGE", "Chỉ nhận ảnh JPG hoặc PNG.");
        if (content.CanSeek) content.Position = 0;
        else throw Error(400, "IMAGE_STREAM_NOT_SEEKABLE", "Không đọc được nội dung ảnh.");

        var dispute = await db.Disputes.Include(x => x.Booking).SingleOrDefaultAsync(x => x.Id == disputeId && x.Booking.CustomerUserId == customerUserId, cancellationToken)
            ?? throw Error(404, "DISPUTE_NOT_FOUND", "Không tìm thấy khiếu nại.");
        if (dispute.Status is DisputeStatus.Decided or DisputeStatus.Closed)
            throw Error(409, "DISPUTE_CLOSED", "Không thể bổ sung bằng chứng cho khiếu nại đã kết thúc.");
        if (await db.DisputeEvidence.CountAsync(x => x.DisputeId == disputeId && x.UploadedByUserId == customerUserId, cancellationToken) >= 10)
            throw Error(409, "EVIDENCE_LIMIT_REACHED", "Mỗi bên có thể gửi tối đa 10 ảnh bằng chứng.");

        var key = await storage.SaveAsync($"disputes/{disputeId:N}", image.Extension, content, cancellationToken);
        try
        {
            var now = clock.GetUtcNow();
            var evidence = new DisputeEvidence
            {
                DisputeId = disputeId,
                UploadedByUserId = customerUserId,
                ObjectKey = key,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.DisputeEvidence.Add(evidence);
            await db.SaveChangesAsync(cancellationToken);
            return ToEvidence(evidence, false);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task<DisputeEvidenceContent?> OpenEvidenceAsync(Guid userId, bool isAdmin, Guid disputeId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var row = await db.DisputeEvidence.AsNoTracking().Where(x => x.Id == evidenceId && x.DisputeId == disputeId
            && (isAdmin || x.Dispute.Booking.CustomerUserId == userId || x.Dispute.Booking.Provider.FreelancerProfile!.UserId == userId))
            .Select(x => new { x.ObjectKey }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var stream = await storage.OpenReadAsync(row.ObjectKey, cancellationToken);
        return stream is null ? null : new DisputeEvidenceContent(stream, KycImage.ContentTypeFromKey(row.ObjectKey));
    }

    public Task<PagedResponse<DisputeResponse>> ListCustomerAsync(Guid customerUserId, int page, int pageSize, CancellationToken cancellationToken) =>
        ListAsync(db.Disputes.AsNoTracking().Where(x => x.Booking.CustomerUserId == customerUserId), page, pageSize, false, customerUserId, cancellationToken);

    public Task<PagedResponse<DisputeResponse>> ListAdminAsync(DisputeStatus? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Disputes.AsNoTracking();
        if (status is { } filter) query = query.Where(x => x.Status == filter);
        return ListAsync(query, page, pageSize, true, Guid.Empty, cancellationToken);
    }

    public Task<DisputeResponse> GetCustomerAsync(Guid customerUserId, Guid disputeId, CancellationToken cancellationToken) => LoadAsync(disputeId, false, customerUserId, cancellationToken);
    public Task<DisputeResponse> GetAdminAsync(Guid disputeId, CancellationToken cancellationToken) => LoadAsync(disputeId, true, Guid.Empty, cancellationToken);

    public Task<DisputeResponse> AddCustomerMessageAsync(Guid customerUserId, Guid disputeId, AddDisputeMessageRequest request, CancellationToken cancellationToken) =>
        AddMessageAsync(customerUserId, disputeId, request, false, false, cancellationToken);

    public Task<DisputeResponse> AddAdminMessageAsync(Guid adminUserId, Guid disputeId, AddDisputeMessageRequest request, bool internalNote, CancellationToken cancellationToken) =>
        AddMessageAsync(adminUserId, disputeId, request, true, internalNote, cancellationToken);

    public async Task<DisputeResponse> DecideAsync(Guid adminUserId, Guid disputeId, DisputeDecisionRequest request, CancellationToken cancellationToken)
    {
        var dispute = await db.Disputes.Include(x => x.Booking).SingleOrDefaultAsync(x => x.Id == disputeId, cancellationToken)
            ?? throw Error(404, "DISPUTE_NOT_FOUND", "Không tìm thấy khiếu nại.");
        if (dispute.Status is DisputeStatus.Decided or DisputeStatus.Closed)
            throw Error(409, "DISPUTE_ALREADY_DECIDED", "Khiếu nại đã có quyết định.");
        if (request.RefundAmountVnd is { } refund && request.ProviderAmountVnd is { } provider && refund + provider > dispute.Booking.BaseAmountVnd + dispute.Booking.SurchargeAmountVnd)
            throw Error(400, "INVALID_DISPUTE_AMOUNT", "Tổng hoàn tiền và khoản trả cho người làm vượt quá giá trị đơn.");
        var now = clock.GetUtcNow();
        db.DisputeDecisions.Add(new DisputeDecision
        {
            DisputeId = disputeId, AdminUserId = adminUserId, Type = DisputeDecisionType.Initial,
            OutcomeCode = request.OutcomeCode.Trim(), Reason = request.Reason.Trim(),
            RefundAmountVnd = request.RefundAmountVnd, ProviderAmountVnd = request.ProviderAmountVnd,
            DecidedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now
        });
        dispute.Status = DisputeStatus.Decided;
        dispute.ClosedAtUtc = now;
        dispute.UpdatedAtUtc = now;
        workflow.Notify(dispute.Booking.CustomerUserId, "dispute.decided", "Khiếu nại đã được xử lý", $"Admin đã cập nhật kết quả khiếu nại cho đơn {dispute.Booking.BookingNumber}.", dispute.Booking, now);
        await db.SaveChangesAsync(cancellationToken);
        return await LoadAsync(disputeId, true, Guid.Empty, cancellationToken);
    }

    private async Task<DisputeResponse> AddMessageAsync(Guid actorId, Guid disputeId, AddDisputeMessageRequest request, bool isAdmin, bool internalNote, CancellationToken cancellationToken)
    {
        var dispute = await db.Disputes.AsNoTracking().Where(x => x.Id == disputeId && (isAdmin || x.Booking.CustomerUserId == actorId)).Select(x => new { x.Id }).SingleOrDefaultAsync(cancellationToken)
            ?? throw Error(404, "DISPUTE_NOT_FOUND", "Không tìm thấy khiếu nại.");
        db.DisputeMessages.Add(new DisputeMessage
        {
            DisputeId = dispute.Id, AuthorUserId = actorId, Body = request.Body.Trim(), IsInternalAdminNote = isAdmin && internalNote,
            CreatedAtUtc = clock.GetUtcNow(), UpdatedAtUtc = clock.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
        return isAdmin ? await GetAdminAsync(disputeId, cancellationToken) : await GetCustomerAsync(actorId, disputeId, cancellationToken);
    }

    private async Task<PagedResponse<DisputeResponse>> ListAsync(IQueryable<Dispute> query, int page, int pageSize, bool admin, Guid userId, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.LongCountAsync(cancellationToken);
        var ids = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToListAsync(cancellationToken);
        var items = new List<DisputeResponse>(ids.Count);
        foreach (var id in ids) items.Add(await LoadAsync(id, admin, userId, cancellationToken));
        return new PagedResponse<DisputeResponse>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    private async Task<DisputeResponse> LoadAsync(Guid id, bool admin, Guid userId, CancellationToken cancellationToken)
    {
        var query = db.Disputes.AsNoTracking().Where(x => x.Id == id);
        if (!admin) query = query.Where(x => x.Booking.CustomerUserId == userId);
        var dispute = await query.Include(x => x.Booking).ThenInclude(x => x.CustomerUser).ThenInclude(x => x.CustomerProfile)
            .Include(x => x.Booking).ThenInclude(x => x.Provider)
            .Include(x => x.Evidence).Include(x => x.Decisions).Include(x => x.Messages)
            .SingleOrDefaultAsync(cancellationToken) ?? throw Error(404, "DISPUTE_NOT_FOUND", "Không tìm thấy khiếu nại.");
        var messages = dispute.Messages.Where(x => admin || !x.IsInternalAdminNote).OrderBy(x => x.CreatedAtUtc)
            .Select(x => new DisputeMessageResponse(x.Id, x.AuthorUserId, x.Body, x.IsInternalAdminNote, x.CreatedAtUtc)).ToArray();
        return new DisputeResponse(dispute.Id, dispute.BookingId, dispute.Booking.BookingNumber, dispute.Booking.Status,
            dispute.Booking.CustomerUserId, dispute.Booking.CustomerUser.CustomerProfile?.FullName ?? string.Empty,
            dispute.Booking.ProviderId, dispute.Booking.Provider.DisplayName, dispute.Category, dispute.Status, dispute.Description,
            dispute.ResponseDeadlineUtc, dispute.CreatedAtUtc, dispute.Evidence.OrderBy(x => x.CreatedAtUtc).Select(x => ToEvidence(x, admin)).ToArray(), messages,
            dispute.Decisions.OrderBy(x => x.DecidedAtUtc).Select(x => new DisputeDecisionResponse(x.Id, x.AdminUserId, x.Type, x.OutcomeCode, x.Reason, x.RefundAmountVnd, x.ProviderAmountVnd, x.DecidedAtUtc)).ToArray());
    }

    private static DisputeEvidenceResponse ToEvidence(DisputeEvidence evidence, bool admin) => new(
        evidence.Id, evidence.UploadedByUserId, evidence.Description, evidence.CreatedAtUtc,
        $"/{ApiRoutes.Version1}/{(admin ? "admin/" : string.Empty)}disputes/{evidence.DisputeId}/evidence/{evidence.Id}/content");

    private static BookingFlowException Error(int status, string code, string message) => new(status, code, message);
}
