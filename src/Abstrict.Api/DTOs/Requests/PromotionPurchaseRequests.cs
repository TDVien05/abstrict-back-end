using System.ComponentModel.DataAnnotations;
using Abstrict.Api.Models.Enums;

namespace Abstrict.Api.DTOs.Requests;

public sealed class PurchasePromotionRequest
{
    [Required]
    public Guid? PlanId { get; init; }

    /// <summary>Phương thức thanh toán; bỏ qua với gói miễn phí.</summary>
    public PaymentMethod Method { get; init; } = PaymentMethod.Momo;
}
