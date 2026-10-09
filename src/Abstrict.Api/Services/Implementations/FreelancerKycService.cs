using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class FreelancerKycService(
    AppDbContext dbContext,
    IFileStorage fileStorage,
    CitizenIdProtector citizenIdProtector,
    TimeProvider timeProvider) : IFreelancerKycService
{
    public const int MinimumAge = 18;

    public static readonly VerificationDocumentType[] RequiredDocumentTypes =
    [
        VerificationDocumentType.CitizenIdFront,
        VerificationDocumentType.CitizenIdBack,
        VerificationDocumentType.FaceVerification
    ];

    public async Task<FreelancerKycResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(userId, track: false, cancellationToken);
        return profile is null ? NotStarted() : await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<FreelancerKycResponse> SaveProfileAsync(Guid userId, SaveFreelancerKycProfileRequest request, CancellationToken cancellationToken)
    {
        var legalName = string.Join(' ', request.LegalFullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (legalName.Length < 2)
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_LEGAL_NAME", "Vui lòng nhập họ tên đúng như trên CCCD.");

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (request.DateOfBirth > today.AddYears(-MinimumAge))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "UNDERAGE", $"Freelancer phải từ {MinimumAge} tuổi trở lên.");
        if (request.DateOfBirth < today.AddYears(-100))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DATE_OF_BIRTH", "Ngày sinh không hợp lệ.");

        var citizenId = citizenIdProtector.Protect(request.CitizenIdNumber);
        var profile = await LoadProfileAsync(userId, track: true, cancellationToken);

        if (profile is not null && !IsEditable(profile.Provider.ApprovalStatus))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_NOT_EDITABLE", "Hồ sơ đã gửi duyệt, không thể chỉnh sửa lúc này.");

        if (await dbContext.FreelancerProfiles.AnyAsync(x => x.CitizenIdHash == citizenId.Hash && x.UserId != userId, cancellationToken))
            throw new KycFlowException(StatusCodes.Status409Conflict, "CITIZEN_ID_ALREADY_USED", "Số CCCD này đã được dùng cho một tài khoản khác.");

        if (profile is null)
        {
            var provider = new Provider
            {
                Type = ProviderType.Freelancer,
                DisplayName = legalName,
                ApprovalStatus = ApprovalStatus.Draft
            };
            profile = new FreelancerProfile
            {
                Provider = provider,
                UserId = userId,
                LegalFullName = legalName,
                PermanentAddress = string.Empty,
                CurrentAddress = string.Empty
            };
            dbContext.Providers.Add(provider);
            dbContext.FreelancerProfiles.Add(profile);
        }

        var now = timeProvider.GetUtcNow();
        profile.LegalFullName = legalName;
        profile.DateOfBirth = request.DateOfBirth;
        profile.Gender = request.Gender;
        profile.PermanentAddress = request.PermanentAddress.Trim();
        profile.CurrentAddress = request.CurrentAddress.Trim();
        profile.ExperienceYears = request.ExperienceYears;
        profile.CitizenIdNumberProtected = citizenId.Protected;
        profile.CitizenIdHash = citizenId.Hash;
        profile.CitizenIdLast4 = citizenId.Last4;
        profile.UpdatedAtUtc = now;
        profile.Provider.DisplayName = legalName;
        profile.Provider.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Chỉ mục duy nhất trên hash CCCD hoặc user: hai request song song cùng CCCD.
            throw new KycFlowException(StatusCodes.Status409Conflict, "CITIZEN_ID_ALREADY_USED", "Số CCCD này đã được dùng cho một tài khoản khác.");
        }

        return await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<KycDocumentResponse> UploadDocumentAsync(
        Guid userId, VerificationDocumentType type, string originalFileName, long length, Stream content, CancellationToken cancellationToken)
    {
        if (!RequiredDocumentTypes.Contains(type))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_TYPE", "Loại giấy tờ không hợp lệ.");
        if (length <= 0)
            throw new KycFlowException(StatusCodes.Status400BadRequest, "EMPTY_FILE", "Tệp tải lên đang trống.");
        if (length > KycImage.MaximumBytes)
            throw new KycFlowException(StatusCodes.Status413PayloadTooLarge, "FILE_TOO_LARGE", "Ảnh không được vượt quá 5 MB.");

        var profile = await LoadProfileAsync(userId, track: true, cancellationToken)
            ?? throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_PROFILE_REQUIRED", "Hãy điền thông tin cá nhân trước khi tải giấy tờ.");
        if (!IsEditable(profile.Provider.ApprovalStatus))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_NOT_EDITABLE", "Hồ sơ đã gửi duyệt, không thể thay đổi giấy tờ lúc này.");

        var header = new byte[8];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var image = KycImage.Detect(header.AsSpan(0, read))
            ?? throw new KycFlowException(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_IMAGE", "Chỉ chấp nhận ảnh JPG hoặc PNG.");
        content.Position = 0;

        var newKey = await fileStorage.SaveAsync($"kyc/{profile.ProviderId:N}", image.Extension, content, cancellationToken);
        var previous = await dbContext.VerificationDocuments
            .Where(x => x.ProviderId == profile.ProviderId && x.Type == type)
            .ToListAsync(cancellationToken);

        var document = new VerificationDocument
        {
            ProviderId = profile.ProviderId,
            Type = type,
            Status = VerificationStatus.Pending,
            ObjectKey = newKey,
            OriginalFileName = SanitizeFileName(originalFileName)
        };
        dbContext.VerificationDocuments.RemoveRange(previous);
        dbContext.VerificationDocuments.Add(document);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(newKey, CancellationToken.None);
            throw;
        }

        foreach (var old in previous)
            await fileStorage.DeleteAsync(old.ObjectKey, CancellationToken.None);

        return document.ToResponse();
    }

    public async Task<FreelancerKycResponse> SubmitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(userId, track: true, cancellationToken)
            ?? throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_PROFILE_REQUIRED", "Hãy điền thông tin cá nhân trước khi gửi duyệt.");
        if (!IsEditable(profile.Provider.ApprovalStatus))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_NOT_EDITABLE", "Hồ sơ đã được gửi duyệt.");
        if (string.IsNullOrEmpty(profile.CitizenIdHash))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_PROFILE_REQUIRED", "Hãy điền thông tin cá nhân trước khi gửi duyệt.");

        var documents = await dbContext.VerificationDocuments
            .Where(x => x.ProviderId == profile.ProviderId).ToListAsync(cancellationToken);
        if (RequiredDocumentTypes.Any(type => documents.All(x => x.Type != type)))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_DOCUMENTS_MISSING", "Bạn cần tải đủ mặt trước CCCD, mặt sau CCCD và ảnh chân dung.");
        if (documents.Any(x => x.Status == VerificationStatus.Rejected))
            throw new KycFlowException(StatusCodes.Status409Conflict, "KYC_DOCUMENT_REJECTED", "Một số giấy tờ bị từ chối. Hãy tải lại ảnh mới trước khi gửi.");

        var now = timeProvider.GetUtcNow();
        profile.Provider.ApprovalStatus = ApprovalStatus.Submitted;
        profile.Provider.UpdatedAtUtc = now;
        dbContext.ProviderApprovalReviews.Add(new ProviderApprovalReview
        {
            ProviderId = profile.ProviderId,
            Decision = ProviderApprovalDecision.Pending,
            SubmittedAtUtc = now
        });
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            Action = "kyc.submitted",
            EntityType = nameof(Provider),
            EntityId = profile.ProviderId
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<KycDocumentContent?> OpenDocumentAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var document = await dbContext.VerificationDocuments.AsNoTracking()
            .Where(d => d.Id == documentId)
            .Where(d => dbContext.FreelancerProfiles.Any(f => f.ProviderId == d.ProviderId && f.UserId == userId))
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        return stream is null ? null : new KycDocumentContent(stream, KycImage.ContentTypeFromKey(document.ObjectKey));
    }

    private static bool IsEditable(ApprovalStatus status) => status is ApprovalStatus.Draft or ApprovalStatus.Rejected;

    private static FreelancerKycResponse NotStarted() =>
        new("NotStarted", true, null, [], null, null);

    private Task<FreelancerProfile?> LoadProfileAsync(Guid userId, bool track, CancellationToken cancellationToken)
    {
        var query = dbContext.FreelancerProfiles.Include(x => x.Provider).AsQueryable();
        if (!track)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    private async Task<FreelancerKycResponse> BuildResponseAsync(FreelancerProfile profile, CancellationToken cancellationToken)
    {
        var documents = await dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.ProviderId == profile.ProviderId)
            .OrderBy(x => x.Type)
            .ToListAsync(cancellationToken);
        var reviews = await dbContext.ProviderApprovalReviews.AsNoTracking()
            .Where(x => x.ProviderId == profile.ProviderId)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        var status = profile.Provider.ApprovalStatus;
        var hasProfileData = !string.IsNullOrEmpty(profile.CitizenIdHash);
        return new FreelancerKycResponse(
            status.ToString(),
            IsEditable(status),
            hasProfileData
                ? new KycProfileResponse(profile.LegalFullName, profile.DateOfBirth, profile.Gender.ToString(),
                    profile.CitizenIdLast4, profile.PermanentAddress, profile.CurrentAddress, profile.ExperienceYears)
                : null,
            documents.Select(x => x.ToResponse()).ToList(),
            KycMapping.ToFeedback(reviews.FirstOrDefault(x => x.DecidedAtUtc.HasValue)),
            reviews.FirstOrDefault()?.SubmittedAtUtc);
    }

    private static string SanitizeFileName(string name)
    {
        var fileName = Path.GetFileName(name ?? string.Empty);
        fileName = new string(fileName.Where(c => !char.IsControl(c)).ToArray());
        return fileName.Length == 0 ? "document" : fileName.Length > 200 ? fileName[^200..] : fileName;
    }
}
