using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abstrict.Api.Services.Implementations;

/// <summary>Phần hồ sơ công ty đã nạp từ DB, dùng chung cho màn hình công ty, admin và response đăng nhập.</summary>
public sealed record CompanyOnboardingSnapshot(
    IReadOnlyList<CompanyServiceAreaResponse> ServiceAreas,
    IReadOnlyList<VerificationDocument> Documents,
    IReadOnlyList<ProviderApprovalReview> Reviews)
{
    public CompanyOnboardingStep ResolveStep(CompanyProfile profile) => CompanyOnboardingPolicy.ResolveStep(
        profile.Provider.ApprovalStatus, profile.Representative is not null, ServiceAreas.Count > 0, profile.LiabilityCoverageVnd,
        Documents.Select(x => (x.Type, x.Status)).ToList());

    public List<CompanyDocumentResponse> DocumentResponses() => Documents
        .Select(x => new CompanyDocumentResponse(
            x.Id, x.Type, x.Status, x.OriginalFileName, x.ContentType ?? CompanyDocument.ContentTypeFromKey(x.ObjectKey),
            x.CreatedAtUtc, x.RejectionReason))
        .ToList();

    public KycFeedbackResponse? Feedback() => KycMapping.ToFeedback(Reviews.FirstOrDefault(x => x.DecidedAtUtc.HasValue));

    public DateTimeOffset? SubmittedAtUtc => Reviews.FirstOrDefault()?.SubmittedAtUtc;
}

public sealed class CompanyOnboardingService(
    AppDbContext dbContext,
    IFileStorage fileStorage,
    CitizenIdProtector citizenIdProtector,
    TimeProvider timeProvider) : ICompanyOnboardingService
{
    public const int MaximumServiceAreas = 20;
    private const string NotStartedStatus = "NotStarted";

    public async Task<CompanyOnboardingResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(dbContext, userId, track: false, cancellationToken);
        return profile is null ? NotStarted() : await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<CompanyOnboardingResponse> SaveCompanyAsync(Guid userId, SaveCompanyProfileRequest request, CancellationToken cancellationToken)
    {
        var legalName = CollapseSpaces(request.LegalName);
        var displayName = CollapseSpaces(request.DisplayName);
        if (legalName.Length < 2 || displayName.Length < 2)
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_COMPANY_NAME", "Tên công ty không hợp lệ.");
        var taxCode = CompanyTaxCode.Normalize(request.TaxCode)
            ?? throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_TAX_CODE",
                "Mã số thuế gồm 10 chữ số, hoặc 10 chữ số kèm hậu tố chi nhánh dạng -001.");
        var hotline = CompanyHotline.Normalize(request.Hotline)
            ?? throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_HOTLINE", "Số hotline không hợp lệ.");

        var profile = await LoadProfileAsync(dbContext, userId, track: true, cancellationToken);
        EnsureEditable(profile);

        if (await dbContext.CompanyProfiles.AnyAsync(x => x.TaxCode == taxCode && x.OwnerUserId != userId, cancellationToken))
            throw TaxCodeAlreadyUsed();

        var now = timeProvider.GetUtcNow();
        if (profile is null)
        {
            var provider = new Provider
            {
                Type = ProviderType.Company,
                DisplayName = displayName,
                ApprovalStatus = ApprovalStatus.Draft,
                IsAcceptingBookings = false,
                CreatedAtUtc = now
            };
            profile = new CompanyProfile
            {
                Provider = provider,
                OwnerUserId = userId,
                LegalName = legalName,
                TaxCode = taxCode,
                Hotline = hotline,
                HeadquartersAddress = string.Empty,
                CreatedAtUtc = now
            };
            dbContext.Providers.Add(provider);
            dbContext.CompanyProfiles.Add(profile);
        }

        profile.LegalName = legalName;
        profile.TaxCode = taxCode;
        profile.Hotline = hotline;
        profile.HeadquartersAddress = request.HeadquartersAddress.Trim();
        profile.WorkforceSize = request.WorkforceSize;
        profile.LiabilityCoverageVnd = request.LiabilityCoverageVnd;
        profile.UpdatedAtUtc = now;
        profile.Provider.DisplayName = displayName;
        profile.Provider.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation } postgres && (postgres.ConstraintName?.Contains("tax_code") ?? false))
        {
            throw TaxCodeAlreadyUsed();
        }

        return await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<CompanyOnboardingResponse> SaveRepresentativeAsync(Guid userId, SaveCompanyRepresentativeRequest request, CancellationToken cancellationToken)
    {
        var phoneNumber = VietnamesePhoneNumber.Normalize(request.PhoneNumber)
            ?? throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_PHONE_NUMBER", "Số điện thoại người đại diện không hợp lệ.");

        var profile = await RequireEditableProfileAsync(userId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var representative = profile.Representative;
        if (representative is null)
        {
            representative = new CompanyRepresentative
            {
                CompanyProfileId = profile.Id,
                FullName = string.Empty,
                JobTitle = string.Empty,
                WorkEmail = string.Empty,
                PhoneNumber = string.Empty,
                IdentityNumberEncrypted = string.Empty,
                CreatedAtUtc = now
            };
            profile.Representative = representative;
            dbContext.CompanyRepresentatives.Add(representative);
        }

        representative.FullName = CollapseSpaces(request.FullName);
        representative.JobTitle = CollapseSpaces(request.JobTitle);
        representative.WorkEmail = request.WorkEmail.Trim().ToLowerInvariant();
        representative.PhoneNumber = phoneNumber;
        representative.IdentityNumberEncrypted = citizenIdProtector.Protect(request.IdentityNumber).Protected;
        representative.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<CompanyOnboardingResponse> SaveServiceAreasAsync(Guid userId, SaveCompanyServiceAreasRequest request, CancellationToken cancellationToken)
    {
        var requested = request.ServiceAreaIds.Distinct().ToList();
        if (requested.Count is 0 or > MaximumServiceAreas)
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_SERVICE_AREA",
                $"Cần chọn từ 1 đến {MaximumServiceAreas} khu vực.");

        var profile = await RequireEditableProfileAsync(userId, cancellationToken);
        var validCount = await dbContext.ServiceAreas.CountAsync(x => requested.Contains(x.Id) && x.IsActive, cancellationToken);
        if (validCount != requested.Count)
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_SERVICE_AREA", "Có khu vực không tồn tại hoặc đã ngừng hoạt động.");

        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.ProviderServiceAreas.Where(x => x.ProviderId == profile.ProviderId).ToListAsync(cancellationToken);
        dbContext.ProviderServiceAreas.RemoveRange(existing.Where(x => !requested.Contains(x.ServiceAreaId)));
        foreach (var areaId in requested.Except(existing.Select(x => x.ServiceAreaId)))
        {
            dbContext.ProviderServiceAreas.Add(new ProviderServiceArea
            {
                ProviderId = profile.ProviderId,
                ServiceAreaId = areaId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<CompanyOnboardingResponse> UploadDocumentAsync(
        Guid userId, VerificationDocumentType type, string originalFileName, long length, Stream content, CancellationToken cancellationToken)
    {
        if (!CompanyDocument.AllTypes.Contains(type))
            throw new KycFlowException(StatusCodes.Status400BadRequest, "INVALID_DOCUMENT_TYPE", "Loại giấy tờ không hợp lệ.");
        if (length <= 0)
            throw new KycFlowException(StatusCodes.Status400BadRequest, "EMPTY_FILE", "Tệp tải lên đang trống.");
        if (length > CompanyDocument.MaximumBytes)
            throw new KycFlowException(StatusCodes.Status413PayloadTooLarge, "FILE_TOO_LARGE", "Tệp không được vượt quá 10 MB.");

        var profile = await RequireEditableProfileAsync(userId, cancellationToken);

        var header = new byte[8];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        var format = CompanyDocument.Detect(type, header.AsSpan(0, read))
            ?? throw new KycFlowException(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_DOCUMENT",
                CompanyDocument.AllowsPdf(type) ? "Chỉ chấp nhận tệp JPG, PNG hoặc PDF." : "Chỉ chấp nhận ảnh JPG hoặc PNG.");
        content.Position = 0;

        var newKey = await fileStorage.SaveAsync($"company/{profile.ProviderId:N}", format.Extension, content, cancellationToken);
        var previous = await dbContext.VerificationDocuments
            .Where(x => x.ProviderId == profile.ProviderId && x.Type == type)
            .ToListAsync(cancellationToken);

        dbContext.VerificationDocuments.RemoveRange(previous);
        dbContext.VerificationDocuments.Add(new VerificationDocument
        {
            ProviderId = profile.ProviderId,
            Type = type,
            Status = VerificationStatus.Pending,
            ObjectKey = newKey,
            OriginalFileName = SanitizeFileName(originalFileName),
            ContentType = format.ContentType,
            ByteSize = length
        });

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

        return await BuildResponseAsync(profile, cancellationToken);
    }

    public async Task<CompanyOnboardingResponse> SubmitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(dbContext, userId, track: true, cancellationToken)
            ?? throw ProfileRequired();
        if (!CompanyOnboardingPolicy.IsEditable(profile.Provider.ApprovalStatus))
            throw new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_NOT_EDITABLE", "Hồ sơ đã được gửi duyệt.");

        var snapshot = await LoadSnapshotAsync(dbContext, profile.ProviderId, cancellationToken);
        switch (snapshot.ResolveStep(profile))
        {
            case CompanyOnboardingStep.Representative:
                throw new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_REPRESENTATIVE_REQUIRED", "Hãy khai thông tin người đại diện trước khi gửi duyệt.");
            case CompanyOnboardingStep.ServiceAreas:
                throw new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_SERVICE_AREAS_REQUIRED", "Hãy chọn ít nhất một khu vực nhận ca trước khi gửi duyệt.");
            case CompanyOnboardingStep.Documents:
                throw snapshot.Documents.Any(x => x.Status == VerificationStatus.Rejected)
                    ? new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_DOCUMENT_REJECTED", "Một số giấy tờ bị từ chối. Hãy tải lại bản mới trước khi gửi.")
                    : new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_DOCUMENTS_MISSING", "Bạn cần tải đủ các giấy tờ bắt buộc trước khi gửi duyệt.");
        }

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
            Action = "company.submitted",
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
            .Where(d => dbContext.CompanyProfiles.Any(c => c.ProviderId == d.ProviderId && c.OwnerUserId == userId))
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null)
            return null;

        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        return stream is null ? null : new KycDocumentContent(stream, CompanyDocument.ContentTypeFromKey(document.ObjectKey));
    }

    /// <summary>Tiến độ rút gọn cho response đăng nhập; dùng cùng cách tính với màn hình onboarding.</summary>
    public static async Task<CompanyOnboardingSummaryResponse> SummarizeAsync(AppDbContext dbContext, Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(dbContext, userId, track: false, cancellationToken);
        if (profile is null)
            return new CompanyOnboardingSummaryResponse(null, NotStartedStatus, CompanyOnboardingStep.Company);

        var snapshot = await LoadSnapshotAsync(dbContext, profile.ProviderId, cancellationToken);
        return new CompanyOnboardingSummaryResponse(profile.ProviderId, profile.Provider.ApprovalStatus.ToString(), snapshot.ResolveStep(profile));
    }

    public static async Task<CompanyOnboardingSnapshot> LoadSnapshotAsync(AppDbContext dbContext, Guid providerId, CancellationToken cancellationToken)
    {
        var areas = await dbContext.ProviderServiceAreas.AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .OrderBy(x => x.ServiceArea.City).ThenBy(x => x.ServiceArea.District).ThenBy(x => x.ServiceArea.WardOrComplex)
            .Select(x => new CompanyServiceAreaResponse(x.ServiceAreaId, x.ServiceArea.City, x.ServiceArea.District, x.ServiceArea.WardOrComplex))
            .ToListAsync(cancellationToken);
        var documents = await dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .OrderBy(x => x.Type)
            .ToListAsync(cancellationToken);
        var reviews = await dbContext.ProviderApprovalReviews.AsNoTracking()
            .Where(x => x.ProviderId == providerId)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
        return new CompanyOnboardingSnapshot(areas, documents, reviews);
    }

    public static CompanyProfileDetailResponse ToCompanyResponse(CompanyProfile profile) => new(
        profile.LegalName, profile.Provider.DisplayName, profile.TaxCode, profile.Hotline, profile.HeadquartersAddress,
        profile.WorkforceSize, profile.LiabilityCoverageVnd);

    private static Task<CompanyProfile?> LoadProfileAsync(AppDbContext dbContext, Guid userId, bool track, CancellationToken cancellationToken)
    {
        var query = dbContext.CompanyProfiles.Include(x => x.Provider).Include(x => x.Representative).AsQueryable();
        if (!track)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.OwnerUserId == userId && x.Provider.Type == ProviderType.Company, cancellationToken);
    }

    private async Task<CompanyProfile> RequireEditableProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await LoadProfileAsync(dbContext, userId, track: true, cancellationToken) ?? throw ProfileRequired();
        EnsureEditable(profile);
        return profile;
    }

    private static void EnsureEditable(CompanyProfile? profile)
    {
        if (profile is not null && !CompanyOnboardingPolicy.IsEditable(profile.Provider.ApprovalStatus))
            throw new KycFlowException(StatusCodes.Status409Conflict, "COMPANY_NOT_EDITABLE", "Hồ sơ đã gửi duyệt, không thể chỉnh sửa lúc này.");
    }

    private async Task<CompanyOnboardingResponse> BuildResponseAsync(CompanyProfile profile, CancellationToken cancellationToken)
    {
        var snapshot = await LoadSnapshotAsync(dbContext, profile.ProviderId, cancellationToken);
        var status = profile.Provider.ApprovalStatus;
        var representative = profile.Representative;
        return new CompanyOnboardingResponse(
            status.ToString(),
            snapshot.ResolveStep(profile),
            CompanyOnboardingPolicy.IsEditable(status),
            profile.ProviderId,
            ToCompanyResponse(profile),
            representative is null
                ? null
                : new CompanyRepresentativeResponse(
                    representative.FullName, representative.JobTitle, representative.WorkEmail, representative.PhoneNumber,
                    citizenIdProtector.Unprotect(representative.IdentityNumberEncrypted)[^4..]),
            snapshot.ServiceAreas,
            CompanyOnboardingPolicy.RequiredDocumentTypes(profile.LiabilityCoverageVnd),
            snapshot.DocumentResponses(),
            snapshot.Feedback(),
            snapshot.SubmittedAtUtc);
    }

    private static CompanyOnboardingResponse NotStarted() => new(
        NotStartedStatus, CompanyOnboardingStep.Company, true, null, null, null, [],
        CompanyOnboardingPolicy.RequiredDocumentTypes(null), [], null, null);

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string SanitizeFileName(string name)
    {
        var fileName = Path.GetFileName(name ?? string.Empty);
        fileName = new string(fileName.Where(c => !char.IsControl(c)).ToArray());
        return fileName.Length == 0 ? "document" : fileName.Length > 200 ? fileName[^200..] : fileName;
    }

    private static KycFlowException ProfileRequired() =>
        new(StatusCodes.Status409Conflict, "COMPANY_PROFILE_REQUIRED", "Hãy khai thông tin công ty trước.");

    private static KycFlowException TaxCodeAlreadyUsed() =>
        new(StatusCodes.Status409Conflict, "TAX_CODE_ALREADY_USED", "Mã số thuế này đã được đăng ký cho một công ty khác.");
}
