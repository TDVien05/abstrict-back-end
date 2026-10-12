namespace Abstrict.Api.Options;

public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    public int SlotStepMinutes { get; set; } = 30;
    public int MinimumDurationMinutes { get; set; } = 120;
    public int MaximumDurationMinutes { get; set; } = 720; // TODO: POLICY
    public int MinimumLeadMinutes { get; set; } = 60; // TODO: POLICY
    public int MaximumAdvanceDays { get; set; } = 30; // TODO: POLICY
    public int DepositPercent { get; set; } = 15;
    public int HoldMinutes { get; set; } = 15; // TODO: POLICY
    public int ProviderConfirmationMinutes { get; set; } = 30; // TODO: POLICY
    public int FreeCancellationMinutes { get; set; } = 120;
    public int LateCancellationProviderPercent { get; set; } = 10;
    public int MaximumActiveHoldsPerCustomer { get; set; } = 3; // TODO: POLICY
    public int CheckInTokenSeconds { get; set; } = 120; // TODO: POLICY
    public int CheckInOpensMinutesBeforeStart { get; set; } = 30; // TODO: POLICY
    public int ExpiryJobIntervalSeconds { get; set; } = 60;
}
