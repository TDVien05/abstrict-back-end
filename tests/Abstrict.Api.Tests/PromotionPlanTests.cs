using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.Tests;

public sealed class PromotionPlanPolicyTests
{
    [Theory]
    [InlineData(" freelancer_featured_7d ", "FREELANCER_FEATURED_7D")]
    [InlineData("A1B", "A1B")]
    public void Code_IsTrimmedAndUppercased(string input, string expected) =>
        Assert.Equal(expected, PromotionPlanPolicy.NormalizeCode(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("_ABC")]
    [InlineData("GÓI 7 NGÀY")]
    [InlineData("HAS SPACE")]
    public void Code_RejectsInvalidValues(string? input)
    {
        var error = Assert.Throws<BookingFlowException>(() => PromotionPlanPolicy.NormalizeCode(input));
        Assert.Equal("INVALID_PLAN_CODE", error.Code);
    }

    [Fact]
    public void Name_CollapsesWhitespaceAndRejectsBlank()
    {
        Assert.Equal("Gói nổi bật 7 ngày", PromotionPlanPolicy.NormalizeName("  Gói  nổi bật   7 ngày "));
        Assert.Equal("INVALID_PLAN_NAME", Assert.Throws<BookingFlowException>(() => PromotionPlanPolicy.NormalizeName("   ")).Code);
    }

    [Fact]
    public void Values_AcceptCompanyPlanWithWorkerLimit() =>
        PromotionPlanPolicy.EnsureValidValues(PromotionAudience.Company, 30, 149_000, 20, 5);

    [Theory]
    [InlineData(0, 1000, null, null, "INVALID_PLAN_DURATION")]
    [InlineData(366, 1000, null, null, "INVALID_PLAN_DURATION")]
    [InlineData(7, -1, null, null, "INVALID_PLAN_PRICE")]
    [InlineData(7, 100_000_001, null, null, "INVALID_PLAN_PRICE")]
    [InlineData(7, 1000, 0, null, "INVALID_PRIORITY_RADIUS")]
    [InlineData(7, 1000, 101, null, "INVALID_PRIORITY_RADIUS")]
    public void Values_RejectOutOfRange(int days, long price, int? radius, int? workers, string code)
    {
        var error = Assert.Throws<BookingFlowException>(() =>
            PromotionPlanPolicy.EnsureValidValues(PromotionAudience.Freelancer, days, price, radius, workers));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void WorkerLimit_NotAllowedForFreelancerPlans() =>
        Assert.Equal("WORKER_LIMIT_NOT_ALLOWED", Assert.Throws<BookingFlowException>(() =>
            PromotionPlanPolicy.EnsureValidValues(PromotionAudience.Freelancer, 7, 1000, null, 3)).Code);

    [Fact]
    public void WorkerLimit_MustBePositiveForCompanyPlans() =>
        Assert.Equal("INVALID_WORKER_LIMIT", Assert.Throws<BookingFlowException>(() =>
            PromotionPlanPolicy.EnsureValidValues(PromotionAudience.Company, 7, 1000, null, 0)).Code);

    [Fact]
    public void Benefits_DropBlankLinesAndRoundTripThroughJson()
    {
        var benefits = PromotionPlanPolicy.NormalizeBenefits([" Ưu tiên hiển thị ", "", "  ", "Huy hiệu nổi bật"]);
        Assert.Equal(["Ưu tiên hiển thị", "Huy hiệu nổi bật"], benefits);
        Assert.Equal(benefits, PromotionPlanPolicy.ParseBenefits(PromotionPlanPolicy.SerializeBenefits(benefits)));
        Assert.Null(PromotionPlanPolicy.SerializeBenefits([]));
    }

    [Fact]
    public void Benefits_RejectTooManyOrTooLong()
    {
        Assert.Equal("INVALID_PLAN_BENEFITS", Assert.Throws<BookingFlowException>(() =>
            PromotionPlanPolicy.NormalizeBenefits(Enumerable.Repeat("x", 11).ToList())).Code);
        Assert.Equal("INVALID_PLAN_BENEFITS", Assert.Throws<BookingFlowException>(() =>
            PromotionPlanPolicy.NormalizeBenefits([new string('x', 201)])).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    public void ParseBenefits_ToleratesBadStoredData(string? json) =>
        Assert.Empty(PromotionPlanPolicy.ParseBenefits(json));

    [Fact]
    public void Audience_FollowsPartnerRole()
    {
        Assert.Equal(PromotionAudience.Freelancer, PromotionPlanPolicy.AudienceOf(UserRole.Freelancer));
        Assert.Equal(PromotionAudience.Company, PromotionPlanPolicy.AudienceOf(UserRole.Company));
        Assert.Equal(403, Assert.Throws<BookingFlowException>(() => PromotionPlanPolicy.AudienceOf(UserRole.Customer)).StatusCode);
    }
}
