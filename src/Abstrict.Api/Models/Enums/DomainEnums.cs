namespace Abstrict.Api.Models.Enums;

public enum AccountStatus { Pending, Active, Suspended, Closed }
public enum OtpPurpose { CustomerRegistration, PasswordlessLogin, PasswordReset }
public enum OtpDeliveryChannel { Sms, ZaloZns }
public enum ProviderType { Freelancer, Company }
public enum ApprovalStatus { Draft, Submitted, UnderReview, Approved, Rejected, Suspended }
public enum Gender { Female, Male, Other, PreferNotToSay }
public enum CompanyWorkforceSize { UnderTen, TenToThirty, OverThirty }
public enum VerificationDocumentType
{
    CitizenIdFront,
    CitizenIdBack,
    FaceVerification,
    HealthCertificate,
    CriminalRecord,
    BusinessRegistration,
    LiabilityInsurance,
    Other
}
public enum VerificationStatus { Pending, Verified, Rejected, Expired }
public enum BookingStatus
{
    PendingDeposit,
    PendingProviderConfirmation,
    Confirmed,
    CheckedIn,
    InProgress,
    PendingAcceptance,
    Completed,
    Expired,
    Rejected,
    Cancelled,
    Disputed
}
public enum BookingTaskStatus { Pending, InProgress, Completed, ClarificationRequested }
public enum BookingPhotoType { Before, After, Evidence }
public enum BookingExtensionStatus { Proposed, Accepted, Rejected, Expired, Cancelled }
public enum PaymentPurpose { Deposit, RemainingBalance, Extension, Promotion, Refund }
public enum PaymentStatus { Pending, Processing, Succeeded, Failed, Cancelled, Refunded, PartiallyRefunded }
public enum PaymentMethod { Momo, VietQr, ZaloPay, Card, PartnerWallet, BankTransfer, Cash, Other }
public enum MoneyMovementType
{
    DepositHeld,
    DepositReleased,
    CustomerRefund,
    ProviderEarning,
    PlatformFee,
    PromotionCharge,
    Adjustment,
    ProviderSecurityDepositHeld,
    ProviderSecurityDepositReleased,
    ProviderPenalty,
    ProviderWalletCredit,
    ProviderWalletDebit
}
public enum MoneyMovementStatus { Pending, Held, Posted, Reversed, Failed }
public enum PromotionAudience { Freelancer, Company }
public enum PromotionPurchaseStatus { PendingPayment, Active, Expired, Cancelled, PaymentFailed }
public enum RatingSubjectType { Customer, Provider }
public enum DisputeCategory { ServiceQuality, Payment, Safety, Damage, Loss, Other }
public enum DisputeStatus { Open, AwaitingResponse, UnderReview, Decided, Appealed, Closed }
public enum DisputeDecisionType { Initial, AppealFinal }
public enum ProviderApprovalDecision { Pending, Approved, Rejected, ChangesRequested }
public enum ProviderAssessmentType { IdentityReadiness, SkillTest, CompanyDueDiligence }
public enum ProviderWalletType { Earnings, SecurityDeposit }
public enum ProviderWalletStatus { Active, Frozen, Closed }
