using System.ComponentModel.DataAnnotations;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Options;
using Abstrict.Api.Services.Implementations;

namespace Abstrict.Api.Tests;

public sealed class BookingPolicyTests
{
    private static readonly BookingOptions Options = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 10, 21, hour, minute, 0, TimeSpan.Zero);
    private static TimeRange Range(int fromHour, int toHour) => new(At(fromHour), At(toHour));

    [Fact]
    public void Pricing_MultipliesHourlyRateByDurationAndTakesFifteenPercentDeposit()
    {
        var price = BookingPricing.Calculate(80_000, 150, 15);

        Assert.Equal(200_000, price.BaseAmountVnd);
        Assert.Equal(0, price.SurchargeAmountVnd);
        Assert.Equal(30_000, price.DepositAmountVnd);
        Assert.Equal(170_000, price.RemainingAmountVnd);
    }

    [Fact]
    public void Pricing_RoundsDepositUpToWholeDong()
    {
        // 75.001đ/giờ × 2 giờ = 150.002đ; 15% = 22.500,3đ → 22.501đ.
        var price = BookingPricing.Calculate(75_001, 120, 15);

        Assert.Equal(150_002, price.BaseAmountVnd);
        Assert.Equal(22_501, price.DepositAmountVnd);
    }

    [Fact]
    public void SlotPolicy_AcceptsExactlyTwoHoursAndRejectsShorter()
    {
        BookingSlotPolicy.EnsureValidRange(At(2), At(4), Now, Options);

        var tooShort = Assert.Throws<BookingFlowException>(() => BookingSlotPolicy.EnsureValidRange(At(2), At(3, 30), Now, Options));
        Assert.Equal("DURATION_TOO_SHORT", tooShort.Code);
    }

    [Fact]
    public void SlotPolicy_RejectsTimesOffTheThirtyMinuteStep()
    {
        var offStep = Assert.Throws<BookingFlowException>(() => BookingSlotPolicy.EnsureValidRange(At(2, 15), At(4, 15), Now, Options));
        Assert.Equal("INVALID_TIME_RANGE", offStep.Code);

        var withSeconds = Assert.Throws<BookingFlowException>(
            () => BookingSlotPolicy.EnsureValidRange(At(2).AddSeconds(1), At(4).AddSeconds(1), Now, Options));
        Assert.Equal("INVALID_TIME_RANGE", withSeconds.Code);
    }

    [Fact]
    public void SlotPolicy_EnforcesLeadTimeAdvanceLimitAndMaximumDuration()
    {
        Assert.Equal("START_TOO_SOON", Assert.Throws<BookingFlowException>(
            () => BookingSlotPolicy.EnsureValidRange(Now.AddMinutes(30), Now.AddMinutes(150), Now, Options)).Code);
        Assert.Equal("START_TOO_FAR", Assert.Throws<BookingFlowException>(
            () => BookingSlotPolicy.EnsureValidRange(Now.AddDays(31), Now.AddDays(31).AddHours(2), Now, Options)).Code);
        Assert.Equal("DURATION_TOO_LONG", Assert.Throws<BookingFlowException>(
            () => BookingSlotPolicy.EnsureValidRange(At(2), At(15), Now, Options)).Code);
    }

    [Fact]
    public void SlotPolicy_MergesTouchingWindowsButNotWindowsWithAGap()
    {
        var touching = new[] { Range(1, 5), Range(5, 9) };
        var gapped = new[] { Range(1, 5), Range(6, 11) };

        Assert.Equal(new[] { Range(1, 9) }, BookingSlotPolicy.Merge(touching));
        Assert.True(BookingSlotPolicy.IsWithinContinuousFree(touching, Range(4, 7)));
        Assert.False(BookingSlotPolicy.IsWithinContinuousFree(gapped, Range(4, 7)));
        Assert.False(BookingSlotPolicy.IsWithinContinuousFree(touching, Range(8, 10)));
    }

    [Fact]
    public void SlotPolicy_SubtractsBusyTimeAndDropsFragmentsShorterThanMinimum()
    {
        // Rảnh 01–11, bận 03–04 và 06–10: còn 01–03 (2h), 04–06 (2h); 10–11 (1h) bị bỏ.
        var free = BookingSlotPolicy.Subtract([Range(1, 11)], [Range(3, 4), Range(6, 10)], Options.MinimumDurationMinutes);

        Assert.Equal(new[] { Range(1, 3), Range(4, 6) }, free);
    }

    [Fact]
    public void Cancellation_IsFreeAtExactlyTwoHoursBeforeStart()
    {
        var startsAt = Now.AddMinutes(120);

        var outcome = BookingCancellationPolicy.Evaluate(BookingStatus.Confirmed, startsAt, Now, 200_000, 30_000, Options);

        Assert.Equal(30_000, outcome.RefundVnd);
        Assert.Equal(0, outcome.RetainedVnd);
    }

    [Fact]
    public void Cancellation_LateSplitsDepositBetweenProviderAndPlatform()
    {
        var startsAt = Now.AddMinutes(119);

        var outcome = BookingCancellationPolicy.Evaluate(BookingStatus.Confirmed, startsAt, Now, 200_000, 30_000, Options);

        Assert.Equal(0, outcome.RefundVnd);
        Assert.Equal(20_000, outcome.ProviderShareVnd);
        Assert.Equal(10_000, outcome.PlatformShareVnd);
        Assert.Equal(30_000, outcome.RetainedVnd);
    }

    [Fact]
    public void Cancellation_BeforeProviderConfirmationAlwaysRefundsInFull()
    {
        var unpaid = BookingCancellationPolicy.Evaluate(BookingStatus.PendingDeposit, Now.AddMinutes(10), Now, 200_000, 0, Options);
        var awaiting = BookingCancellationPolicy.Evaluate(BookingStatus.PendingProviderConfirmation, Now.AddMinutes(10), Now, 200_000, 30_000, Options);

        Assert.Equal(new CancellationOutcome(0, 0, 0), unpaid);
        Assert.Equal(new CancellationOutcome(30_000, 0, 0), awaiting);
    }

    [Theory]
    [InlineData(BookingStatus.CheckedIn)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Expired)]
    public void Cancellation_IsRefusedOutsideCancellableStates(BookingStatus status)
    {
        var exception = Assert.Throws<BookingFlowException>(
            () => BookingCancellationPolicy.Evaluate(status, Now.AddDays(1), Now, 200_000, 30_000, Options));
        Assert.Equal("BOOKING_NOT_CANCELLABLE", exception.Code);
    }

    [Theory]
    [InlineData(BookingStatus.PendingDeposit, BookingStatus.Expired, true)]
    [InlineData(BookingStatus.PendingDeposit, BookingStatus.PendingProviderConfirmation, true)]
    [InlineData(BookingStatus.PendingDeposit, BookingStatus.Confirmed, true)]
    [InlineData(BookingStatus.PendingProviderConfirmation, BookingStatus.Rejected, true)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Cancelled, true)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.PendingDeposit, false)]
    [InlineData(BookingStatus.Confirmed, BookingStatus.Rejected, false)]
    [InlineData(BookingStatus.Expired, BookingStatus.Confirmed, false)]
    [InlineData(BookingStatus.Cancelled, BookingStatus.PendingDeposit, false)]
    public void StateMachine_AllowsOnlyDocumentedTransitions(BookingStatus from, BookingStatus to, bool allowed)
    {
        Assert.Equal(allowed, BookingStateMachine.CanTransition(from, to));
        if (!allowed)
            Assert.Equal("BOOKING_INVALID_STATE", Assert.Throws<BookingFlowException>(() => BookingStateMachine.EnsureCanTransition(from, to)).Code);
    }

    [Fact]
    public void CheckInToken_HashIsStableAndNeverContainsTheRawToken()
    {
        var first = CheckInTokens.Generate();
        var second = CheckInTokens.Generate();

        Assert.NotEqual(first, second);
        Assert.Equal(43, first.Length);
        Assert.Equal(CheckInTokens.Hash(first), CheckInTokens.Hash(first));
        Assert.NotEqual(CheckInTokens.Hash(first), CheckInTokens.Hash(second));
        Assert.Equal(64, CheckInTokens.Hash(first).Length);
        Assert.DoesNotContain(first, CheckInTokens.Hash(first));
    }

    [Fact]
    public void BookingNumber_UsesVietnamDateAndUnambiguousAlphabet()
    {
        // 18:30 UTC ngày 20/10 là 01:30 ngày 21/10 theo giờ Việt Nam.
        var number = BookingNumbers.Generate(new DateTimeOffset(2026, 10, 20, 18, 30, 0, TimeSpan.Zero));

        Assert.StartsWith("AB261021", number);
        Assert.Equal(14, number.Length);
        Assert.DoesNotContain(number[8..], character => "01OIL".Contains(character));
    }

    [Fact]
    public void AddressSnapshot_JoinsNonEmptyPartsAndAppendsAccessNote()
    {
        var address = new CustomerAddress
        {
            Label = "Nhà",
            ApartmentNumber = "A-1205",
            Floor = null,
            BuildingName = "Sunrise City",
            StreetAddress = "23 Nguyễn Hữu Thọ",
            Ward = "Tân Hưng",
            District = "Quận 7",
            City = "Hồ Chí Minh",
            AccessInstructions = "Gửi xe ở hầm B1"
        };

        Assert.Equal(
            "A-1205, Sunrise City, 23 Nguyễn Hữu Thọ, Tân Hưng, Quận 7, Hồ Chí Minh (Ghi chú: Gửi xe ở hầm B1)",
            BookingMapping.ComposeAddressSnapshot(address));
    }

    [Fact]
    public void AddressRequest_RequiresCoreFieldsAndLimitsLengths()
    {
        var valid = new SaveCustomerAddressRequest
        {
            Label = "Nhà", ApartmentNumber = "A-1205", BuildingName = "Sunrise City", StreetAddress = "23 Nguyễn Hữu Thọ",
            Ward = "Tân Hưng", District = "Quận 7", City = "Hồ Chí Minh"
        };
        Assert.Empty(Validate(valid));

        var errors = Validate(new SaveCustomerAddressRequest { Label = new string('x', 81) });
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(SaveCustomerAddressRequest.Label)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(SaveCustomerAddressRequest.City)));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(SaveCustomerAddressRequest.District)));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
