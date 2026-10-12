namespace Abstrict.Api.Options;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary>Bật cổng thanh toán giả lập và endpoint xác nhận sandbox. Chỉ dùng cho môi trường thử.</summary>
    public bool SandboxEnabled { get; set; }
}
