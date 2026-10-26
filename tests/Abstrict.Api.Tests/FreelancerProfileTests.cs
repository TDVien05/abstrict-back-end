using System.ComponentModel.DataAnnotations;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.Tests;

public sealed class FreelancerProfilePolicyTests
{
    [Theory]
    [InlineData(10_000)]
    [InlineData(80_000)]
    [InlineData(1_000_000)]
    public void HourlyRate_AcceptsValuesInsideTheAllowedRange(long rate) =>
        FreelancerProfilePolicy.EnsureValidHourlyRate(rate);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9_999)]
    [InlineData(1_000_001)]
    public void HourlyRate_RejectsValuesOutsideTheAllowedRange(long rate)
    {
        var error = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.EnsureValidHourlyRate(rate));
        Assert.Equal("INVALID_HOURLY_RATE", error.Code);
        Assert.Equal(400, error.StatusCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Dọn dẹp gọn gàng  ", "Dọn dẹp gọn gàng")]
    public void NormalizeText_TrimsAndTurnsBlankIntoNull(string? input, string? expected) =>
        Assert.Equal(expected, FreelancerProfilePolicy.NormalizeText(input));

    [Fact]
    public void SuspendedProfile_CannotBeEdited_ButOtherStatusesCan()
    {
        var error = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.EnsureEditable(ApprovalStatus.Suspended));
        Assert.Equal("PROVIDER_SUSPENDED", error.Code);
        Assert.Equal(409, error.StatusCode);

        foreach (var status in Enum.GetValues<ApprovalStatus>().Where(x => x != ApprovalStatus.Suspended))
            FreelancerProfilePolicy.EnsureEditable(status);
    }

    [Fact]
    public void MissingForBookings_IsEmptyOnlyWhenApprovedWithAServiceAndAnArea() =>
        Assert.Empty(FreelancerProfilePolicy.MissingForBookings(ApprovalStatus.Approved, 1, 1));

    [Theory]
    [InlineData(ApprovalStatus.Approved, 0, 1, new[] { "NO_ACTIVE_SERVICE" })]
    [InlineData(ApprovalStatus.Approved, 2, 0, new[] { "NO_SERVICE_AREA" })]
    [InlineData(ApprovalStatus.Approved, 0, 0, new[] { "NO_ACTIVE_SERVICE", "NO_SERVICE_AREA" })]
    [InlineData(ApprovalStatus.Submitted, 1, 1, new[] { "NOT_APPROVED" })]
    [InlineData(ApprovalStatus.Rejected, 0, 0, new[] { "NOT_APPROVED", "NO_ACTIVE_SERVICE", "NO_SERVICE_AREA" })]
    public void MissingForBookings_ListsEveryUnmetCondition(ApprovalStatus status, int services, int areas, string[] expected) =>
        Assert.Equal(expected, FreelancerProfilePolicy.MissingForBookings(status, services, areas));

    [Fact]
    public void EnsureCanAcceptBookings_ReportsWhatIsMissing()
    {
        FreelancerProfilePolicy.EnsureCanAcceptBookings([]);

        var error = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.EnsureCanAcceptBookings(["NO_ACTIVE_SERVICE", "NO_SERVICE_AREA"]));
        Assert.Equal("PROFILE_NOT_READY", error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.Contains("NO_ACTIVE_SERVICE", error.Message);
        Assert.Contains("NO_SERVICE_AREA", error.Message);
    }

    [Fact]
    public void NormalizeServiceAreas_AppliesDefaultRadiusAndKeepsExplicitOnes()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = FreelancerProfilePolicy.NormalizeServiceAreas([
            new FreelancerServiceAreaInput { ServiceAreaId = first },
            new FreelancerServiceAreaInput { ServiceAreaId = second, TravelRadiusKm = 25 }]);

        Assert.Equal([new NormalizedServiceArea(first, 10), new NormalizedServiceArea(second, 25)], result);
    }

    [Fact]
    public void NormalizeServiceAreas_RejectsEmptyAndTooManyAreas()
    {
        var empty = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.NormalizeServiceAreas([]));
        Assert.Equal("INVALID_SERVICE_AREA", empty.Code);

        var tooMany = Enumerable.Range(0, FreelancerProfilePolicy.MaximumServiceAreas + 1)
            .Select(_ => new FreelancerServiceAreaInput { ServiceAreaId = Guid.NewGuid() }).ToList();
        var error = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.NormalizeServiceAreas(tooMany));
        Assert.Equal("INVALID_SERVICE_AREA", error.Code);

        var atLimit = tooMany.Take(FreelancerProfilePolicy.MaximumServiceAreas).ToList();
        Assert.Equal(FreelancerProfilePolicy.MaximumServiceAreas, FreelancerProfilePolicy.NormalizeServiceAreas(atLimit).Count);
    }

    [Fact]
    public void NormalizeServiceAreas_RejectsDuplicates()
    {
        var id = Guid.NewGuid();
        var error = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.NormalizeServiceAreas([
            new FreelancerServiceAreaInput { ServiceAreaId = id }, new FreelancerServiceAreaInput { ServiceAreaId = id, TravelRadiusKm = 5 }]));
        Assert.Equal("DUPLICATE_SERVICE_AREA", error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-5)]
    public void NormalizeServiceAreas_RejectsRadiusOutsideTheRange(int radius)
    {
        var error = Assert.Throws<BookingFlowException>(() => FreelancerProfilePolicy.NormalizeServiceAreas(
            [new FreelancerServiceAreaInput { ServiceAreaId = Guid.NewGuid(), TravelRadiusKm = radius }]));
        Assert.Equal("INVALID_TRAVEL_RADIUS", error.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void NormalizeServiceAreas_AcceptsRadiusAtTheBoundaries(int radius) =>
        Assert.Equal(radius, FreelancerProfilePolicy.NormalizeServiceAreas(
            [new FreelancerServiceAreaInput { ServiceAreaId = Guid.NewGuid(), TravelRadiusKm = radius }])[0].TravelRadiusKm);
}

public sealed class FreelancerProfileRequestValidationTests
{
    [Fact]
    public void UpdateRequest_AllowsAnEmptyPatch_AndLimitsTheBio()
    {
        Assert.Empty(Validate(new UpdateFreelancerProfileRequest()));
        Assert.Empty(Validate(new UpdateFreelancerProfileRequest { Bio = new string('a', FreelancerProfilePolicy.MaximumBioLength) }));
        Assert.Contains(Validate(new UpdateFreelancerProfileRequest { Bio = new string('a', FreelancerProfilePolicy.MaximumBioLength + 1) }),
            error => error.MemberNames.Contains(nameof(UpdateFreelancerProfileRequest.Bio)));
    }

    [Fact]
    public void SaveServiceRequest_RequiresTheRate_AndLimitsTheDescription()
    {
        Assert.Contains(Validate(new SaveFreelancerServiceRequest()), error => error.MemberNames.Contains(nameof(SaveFreelancerServiceRequest.HourlyRateVnd)));
        Assert.Empty(Validate(new SaveFreelancerServiceRequest { HourlyRateVnd = 80_000 }));
        Assert.True(new SaveFreelancerServiceRequest { HourlyRateVnd = 80_000 }.IsActive);
        Assert.Contains(Validate(new SaveFreelancerServiceRequest { HourlyRateVnd = 80_000, Description = new string('a', FreelancerProfilePolicy.MaximumServiceDescriptionLength + 1) }),
            error => error.MemberNames.Contains(nameof(SaveFreelancerServiceRequest.Description)));
    }

    [Fact]
    public void ServiceAreasRequest_RequiresBetweenOneAndTwentyAreas()
    {
        Assert.NotEmpty(Validate(new ReplaceFreelancerServiceAreasRequest()));
        Assert.Empty(Validate(new ReplaceFreelancerServiceAreasRequest { ServiceAreas = [new FreelancerServiceAreaInput { ServiceAreaId = Guid.NewGuid() }] }));

        var tooMany = Enumerable.Range(0, FreelancerProfilePolicy.MaximumServiceAreas + 1).Select(_ => new FreelancerServiceAreaInput { ServiceAreaId = Guid.NewGuid() }).ToList();
        Assert.NotEmpty(Validate(new ReplaceFreelancerServiceAreasRequest { ServiceAreas = tooMany }));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void ServiceAreaInput_ChecksTheRadiusRange(int radius, bool valid) =>
        Assert.Equal(valid, Validate(new FreelancerServiceAreaInput { ServiceAreaId = Guid.NewGuid(), TravelRadiusKm = radius }).Count == 0);

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
