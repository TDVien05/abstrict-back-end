using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.Tests;

public sealed class CompanyOnboardingTests
{
    private static readonly (VerificationDocumentType, VerificationStatus)[] RequiredPending =
    [
        (VerificationDocumentType.BusinessRegistration, VerificationStatus.Pending),
        (VerificationDocumentType.CitizenIdFront, VerificationStatus.Pending),
        (VerificationDocumentType.CitizenIdBack, VerificationStatus.Pending)
    ];

    [Theory]
    [InlineData("0312345678", "0312345678")]
    [InlineData("0312345678-001", "0312345678-001")]
    [InlineData(" 0312 345 678 ", "0312345678")]
    [InlineData("031234567", null)]
    [InlineData("03123456789", null)]
    [InlineData("03123A5678", null)]
    [InlineData("0312345678-01", null)]
    [InlineData("0312345678001", null)]
    [InlineData("", null)]
    public void TaxCode_AcceptsTenDigitsWithOptionalBranchSuffix(string input, string? expected) =>
        Assert.Equal(expected, CompanyTaxCode.Normalize(input));

    [Theory]
    [InlineData("028 3812 3456", "02838123456")]
    [InlineData("0901234567", "0901234567")]
    [InlineData("1900 1234", "19001234")]
    [InlineData("1800-599-920", "1800599920")]
    [InlineData("1900123", null)]
    [InlineData("12345678", null)]
    [InlineData("hotline", null)]
    public void Hotline_AcceptsVietnamesePhoneNumbersAndTollFreeLines(string input, string? expected) =>
        Assert.Equal(expected, CompanyHotline.Normalize(input));

    [Fact]
    public void ResolveStep_WalksThroughOnboardingInOrder()
    {
        Assert.Equal(CompanyOnboardingStep.Company, Resolve(null, false, false, null, []));
        Assert.Equal(CompanyOnboardingStep.Representative, Resolve(ApprovalStatus.Draft, false, false, null, []));
        Assert.Equal(CompanyOnboardingStep.ServiceAreas, Resolve(ApprovalStatus.Draft, true, false, null, RequiredPending));
        Assert.Equal(CompanyOnboardingStep.Documents, Resolve(ApprovalStatus.Draft, true, true, null, RequiredPending[..2]));
        Assert.Equal(CompanyOnboardingStep.Submit, Resolve(ApprovalStatus.Draft, true, true, null, RequiredPending));
    }

    [Fact]
    public void ResolveStep_RequiresInsuranceDocumentOnlyWhenCoverageIsDeclared()
    {
        Assert.Equal(CompanyOnboardingStep.Submit, Resolve(ApprovalStatus.Draft, true, true, 0, RequiredPending));
        Assert.Equal(CompanyOnboardingStep.Documents, Resolve(ApprovalStatus.Draft, true, true, 500_000_000, RequiredPending));

        var withInsurance = RequiredPending.Append((VerificationDocumentType.LiabilityInsurance, VerificationStatus.Pending)).ToArray();
        Assert.Equal(CompanyOnboardingStep.Submit, Resolve(ApprovalStatus.Draft, true, true, 500_000_000, withInsurance));
    }

    [Fact]
    public void ResolveStep_SendsRejectedDocumentsBackToDocumentsStep()
    {
        var withRejected = RequiredPending.ToArray();
        withRejected[0] = (VerificationDocumentType.BusinessRegistration, VerificationStatus.Rejected);

        Assert.Equal(CompanyOnboardingStep.Documents, Resolve(ApprovalStatus.Draft, true, true, null, withRejected));
        Assert.Equal(CompanyOnboardingStep.Documents, Resolve(ApprovalStatus.Rejected, true, true, null, withRejected));
    }

    [Theory]
    [InlineData(ApprovalStatus.Submitted, CompanyOnboardingStep.AwaitingReview)]
    [InlineData(ApprovalStatus.UnderReview, CompanyOnboardingStep.AwaitingReview)]
    [InlineData(ApprovalStatus.Approved, CompanyOnboardingStep.Completed)]
    [InlineData(ApprovalStatus.Suspended, CompanyOnboardingStep.Suspended)]
    public void ResolveStep_ReviewStatesIgnoreMissingData(ApprovalStatus status, CompanyOnboardingStep expected) =>
        Assert.Equal(expected, Resolve(status, false, false, null, []));

    [Theory]
    [InlineData(ApprovalStatus.Draft, true)]
    [InlineData(ApprovalStatus.Rejected, true)]
    [InlineData(ApprovalStatus.Submitted, false)]
    [InlineData(ApprovalStatus.UnderReview, false)]
    [InlineData(ApprovalStatus.Approved, false)]
    public void Profile_IsEditableOnlyBeforeSubmissionOrAfterRejection(ApprovalStatus status, bool editable) =>
        Assert.Equal(editable, CompanyOnboardingPolicy.IsEditable(status));

    [Fact]
    public void Document_DetectsImagesForEveryTypeAndPdfOnlyWhereAllowed()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0];
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        byte[] pdf = "%PDF-1.7"u8.ToArray();

        Assert.Equal(("image/jpeg", ".jpg"), CompanyDocument.Detect(VerificationDocumentType.CitizenIdFront, jpeg));
        Assert.Equal(("image/png", ".png"), CompanyDocument.Detect(VerificationDocumentType.BusinessRegistration, png));
        Assert.Equal(("application/pdf", ".pdf"), CompanyDocument.Detect(VerificationDocumentType.BusinessRegistration, pdf));
        Assert.Equal(("application/pdf", ".pdf"), CompanyDocument.Detect(VerificationDocumentType.LiabilityInsurance, pdf));
        Assert.Null(CompanyDocument.Detect(VerificationDocumentType.CitizenIdFront, pdf));
        Assert.Null(CompanyDocument.Detect(VerificationDocumentType.CitizenIdBack, pdf));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // GIF
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })] // "%PDF" thiếu dấu gạch
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E })] // HTML
    public void Document_RejectsOtherContent(byte[] header) =>
        Assert.Null(CompanyDocument.Detect(VerificationDocumentType.BusinessRegistration, header));

    [Fact]
    public void Document_ContentTypeFollowsStoredExtension()
    {
        Assert.Equal("application/pdf", CompanyDocument.ContentTypeFromKey("company/abc/file.pdf"));
        Assert.Equal("image/png", CompanyDocument.ContentTypeFromKey("company/abc/file.png"));
        Assert.Equal("image/jpeg", CompanyDocument.ContentTypeFromKey("company/abc/file.jpg"));
    }

    private static CompanyOnboardingStep Resolve(
        ApprovalStatus? status, bool hasRepresentative, bool hasServiceAreas, long? coverage,
        (VerificationDocumentType, VerificationStatus)[] documents) =>
        CompanyOnboardingPolicy.ResolveStep(status, hasRepresentative, hasServiceAreas, coverage, documents);
}
