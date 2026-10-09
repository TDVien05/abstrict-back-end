using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;

namespace Abstrict.Api.Services.Implementations;

internal static class KycMapping
{
    public static KycDocumentResponse ToResponse(this VerificationDocument document) =>
        new(document.Id, document.Type.ToString(), document.Status.ToString(), document.OriginalFileName,
            document.CreatedAtUtc, document.RejectionReason);

    public static KycFeedbackResponse? ToFeedback(ProviderApprovalReview? review) =>
        review is { DecidedAtUtc: not null }
            ? new KycFeedbackResponse(review.Decision.ToString(), review.DecisionReason, review.DecidedAtUtc.Value)
            : null;
}
