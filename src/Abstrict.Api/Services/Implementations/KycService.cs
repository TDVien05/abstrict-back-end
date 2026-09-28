using System.Globalization;
using Abstrict.Api.Data;
using Abstrict.Api.Integrations.Identity;
using Abstrict.Api.Integrations.Storage;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Repositories.Interfaces;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abstrict.Api.Services.Implementations;

public sealed class KycService(
    IKycOperationRepository operationRepository,
    IVerificationDocumentRepository documentRepository,
    IIdentityAttemptRepository attemptRepository,
    IUnitOfWork unitOfWork,
    IPrivateFileStorage fileStorage,
    ISensitiveDataProtector dataProtector,
    IIdentityFingerprintService fingerprintService,
    IFptAiIdentityClient fptAiClient,
    IFaceVerificationClient faceClient,
    IOptions<KycOptions> options,
    TimeProvider timeProvider,
    ILogger<KycService> logger) : IKycService
{
    private const int MaxAttempts = 3;
    private readonly KycOptions _kyc = options.Value;

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var operation = await operationRepository.ClaimNextAsync(now, cancellationToken);
        if (operation is null)
            return false;

        try
        {
            if (operation.Type == KycOperationType.IdentityOcr)
                await ProcessOcrAsync(operation, now, cancellationToken);
            else
                await ProcessFaceMatchAsync(operation, now, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "KYC operation {OperationId} failed unexpectedly.", operation.Id);
            await FailAsync(operation, "UNEXPECTED_ERROR", now, cancellationToken);
        }

        return true;
    }

    private async Task ProcessOcrAsync(KycOperation operation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var attempt = await attemptRepository.GetAsync(operation.AttemptId!.Value, track: true, cancellationToken);
        if (attempt is null || attempt.SupersededAtUtc is not null)
        {
            await SupersedeAsync(operation, now, cancellationToken);
            return;
        }

        var front = await LoadDocumentAsync(attempt.FrontDocumentId, cancellationToken);
        var back = await LoadDocumentAsync(attempt.BackDocumentId, cancellationToken);
        if (front is null || back is null)
        {
            await FailAsync(operation, "OCR_INCOMPLETE", now, cancellationToken);
            return;
        }

        var frontResult = await fptAiClient.ReadAsync(front.Value.Stream, front.Value.FileName, includeFaceCrop: true, cancellationToken);
        if (!frontResult.Succeeded)
        {
            await HandleProviderFailureAsync(operation, attempt, frontResult.ErrorCode ?? "KYC_PROVIDER_UNAVAILABLE", now, cancellationToken);
            return;
        }

        var backResult = await fptAiClient.ReadAsync(back.Value.Stream, back.Value.FileName, includeFaceCrop: false, cancellationToken);

        attempt.DocumentNumberEncrypted = string.IsNullOrEmpty(frontResult.DocumentNumber) ? null : dataProtector.Protect(frontResult.DocumentNumber);
        attempt.DocumentNumberLast4 = string.IsNullOrEmpty(frontResult.DocumentNumber) || frontResult.DocumentNumber.Length < 4
            ? frontResult.DocumentNumber
            : frontResult.DocumentNumber[^4..];
        attempt.DocumentNumberFingerprint = string.IsNullOrEmpty(frontResult.DocumentNumber) ? null : fingerprintService.Compute(frontResult.DocumentNumber);
        attempt.FullName = frontResult.FullName;
        attempt.DateOfBirth = TryParseDate(frontResult.DateOfBirth);
        attempt.Gender = ParseGender(frontResult.Gender);
        attempt.PermanentAddress = frontResult.Address;
        attempt.IssuedOn = TryParseDate(frontResult.IssueDate);
        attempt.IssuedPlace = frontResult.IssuePlace;
        attempt.ExpiresOn = TryParseDate(frontResult.ExpiryDate);
        attempt.DocumentType = frontResult.DocumentType;
        attempt.FieldConfidencesJson = System.Text.Json.JsonSerializer.Serialize(frontResult.FieldConfidences);
        attempt.OcrProviderRequestId = frontResult.ProviderRequestId;

        var incomplete = string.IsNullOrWhiteSpace(frontResult.DocumentNumber) || string.IsNullOrWhiteSpace(frontResult.FullName) || attempt.DateOfBirth is null;
        var expired = attempt.ExpiresOn is not null && attempt.ExpiresOn < DateOnly.FromDateTime(now.UtcDateTime);
        attempt.OcrCompletedAtUtc = now;
        attempt.UpdatedAtUtc = now;

        if (expired)
        {
            attempt.OcrState = OcrState.NeedsReview;
            attempt.ResultCode = "ID_EXPIRED";
        }
        else if (incomplete)
        {
            attempt.OcrState = OcrState.NeedsReview;
            attempt.ResultCode = "OCR_INCOMPLETE";
        }
        else
        {
            attempt.OcrState = OcrState.Extracted;
            attempt.ResultCode = null;
        }

        operation.State = KycOperationState.Succeeded;
        operation.ResultJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            attempt.OcrState,
            attempt.DocumentNumberLast4,
            attempt.FullName,
            BackSideRead = backResult.Succeeded
        });
        operation.ProviderRequestId = frontResult.ProviderRequestId;
        operation.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessFaceMatchAsync(KycOperation operation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var attempt = await attemptRepository.GetAsync(operation.AttemptId!.Value, track: true, cancellationToken);
        if (attempt is null || attempt.SupersededAtUtc is not null || attempt.SelfieDocumentId is null)
        {
            await SupersedeAsync(operation, now, cancellationToken);
            return;
        }

        var portrait = await LoadDocumentAsync(attempt.FrontDocumentId, cancellationToken);
        var selfie = await LoadDocumentAsync(attempt.SelfieDocumentId, cancellationToken);
        if (portrait is null || selfie is null)
        {
            await FailAsync(operation, "DOCUMENT_SIDE_INVALID", now, cancellationToken);
            return;
        }

        var detection = await faceClient.DetectAsync(selfie.Value.Stream, selfie.Value.FileName, cancellationToken);
        if (!detection.Succeeded)
        {
            await HandleProviderFailureAsync(operation, attempt, detection.ErrorCode ?? "KYC_PROVIDER_UNAVAILABLE", now, cancellationToken);
            return;
        }
        if (detection.FaceCount == 0)
        {
            attempt.FaceState = FaceState.NeedsReview;
            attempt.ResultCode = "NO_FACE_DETECTED";
            await CompleteWithoutMatchAsync(operation, attempt, now, cancellationToken);
            return;
        }
        if (detection.FaceCount > 1)
        {
            attempt.FaceState = FaceState.NeedsReview;
            attempt.ResultCode = "MULTIPLE_FACES_DETECTED";
            await CompleteWithoutMatchAsync(operation, attempt, now, cancellationToken);
            return;
        }

        var comparison = await faceClient.CompareAsync(portrait.Value.Stream, portrait.Value.FileName, selfie.Value.Stream, selfie.Value.FileName, cancellationToken);
        if (!comparison.Succeeded || comparison.Confidence is null)
        {
            await HandleProviderFailureAsync(operation, attempt, comparison.ErrorCode ?? "FACE_NOT_MATCHED", now, cancellationToken);
            return;
        }

        var threshold = _kyc.FacePlusPlus.MatchThreshold ?? comparison.Threshold ?? 80m;
        attempt.FaceScore = comparison.Confidence;
        attempt.FaceThreshold = threshold;
        attempt.FaceProviderRequestId = comparison.ProviderRequestId;
        attempt.FaceCompletedAtUtc = now;
        attempt.UpdatedAtUtc = now;

        if (comparison.Confidence >= threshold)
        {
            attempt.FaceState = FaceState.Matched;
            attempt.ResultCode = null;
        }
        else
        {
            attempt.FaceState = FaceState.NotMatched;
            attempt.ResultCode = "FACE_NOT_MATCHED";
        }

        operation.State = KycOperationState.Succeeded;
        operation.ResultJson = System.Text.Json.JsonSerializer.Serialize(new { attempt.FaceState, attempt.FaceScore, threshold });
        operation.ProviderRequestId = comparison.ProviderRequestId;
        operation.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task CompleteWithoutMatchAsync(KycOperation operation, IdentityVerificationAttempt attempt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        attempt.FaceCompletedAtUtc = now;
        attempt.UpdatedAtUtc = now;
        operation.State = KycOperationState.Succeeded;
        operation.ResultJson = System.Text.Json.JsonSerializer.Serialize(new { attempt.FaceState, attempt.ResultCode });
        operation.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleProviderFailureAsync(KycOperation operation, IdentityVerificationAttempt attempt, string errorCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var transient = errorCode is "KYC_PROVIDER_UNAVAILABLE";
        if (transient && operation.AttemptCount < MaxAttempts)
        {
            operation.State = KycOperationState.Queued;
            operation.LeaseExpiresAtUtc = null;
            var backoffSeconds = Math.Pow(2, operation.AttemptCount) * 5;
            operation.NextAttemptAtUtc = now.AddSeconds(backoffSeconds);
            operation.UpdatedAtUtc = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (operation.Type == KycOperationType.IdentityOcr)
        {
            attempt.OcrState = OcrState.Failed;
            attempt.ResultCode = errorCode;
        }
        else
        {
            attempt.FaceState = FaceState.TechnicalError;
            attempt.ResultCode = errorCode;
        }
        attempt.UpdatedAtUtc = now;

        operation.State = KycOperationState.Failed;
        operation.ErrorCode = errorCode;
        operation.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(KycOperation operation, string errorCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        operation.State = KycOperationState.Failed;
        operation.ErrorCode = errorCode;
        operation.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SupersedeAsync(KycOperation operation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        operation.State = KycOperationState.Superseded;
        operation.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Stream Stream, string FileName)?> LoadDocumentAsync(Guid? documentId, CancellationToken cancellationToken)
    {
        if (documentId is null)
            return null;
        var document = await documentRepository.GetAsync(documentId.Value, track: false, cancellationToken);
        if (document is null)
            return null;
        var stream = await fileStorage.OpenReadAsync(document.ObjectKey, cancellationToken);
        return stream is null ? null : (stream, document.OriginalFileName);
    }

    private static DateOnly? TryParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "dd-MM-yyyy", "yyyy/MM/dd", "dd.MM.yyyy"];
        return DateOnly.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static Gender? ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "nam" or "male" or "m" => Gender.Male,
            "nữ" or "nu" or "female" or "f" => Gender.Female,
            "khác" or "other" => Gender.Other,
            _ => null
        };
    }
}
