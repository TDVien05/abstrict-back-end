using Abstrict.Api.Models.Entities;
using Abstrict.Api.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PhoneOtpChallenge> PhoneOtpChallenges => Set<PhoneOtpChallenge>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<FreelancerProfile> FreelancerProfiles => Set<FreelancerProfile>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<CompanyRepresentative> CompanyRepresentatives => Set<CompanyRepresentative>();
    public DbSet<CompanyWorker> CompanyWorkers => Set<CompanyWorker>();
    public DbSet<VerificationDocument> VerificationDocuments => Set<VerificationDocument>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<ProviderApprovalReview> ProviderApprovalReviews => Set<ProviderApprovalReview>();
    public DbSet<ProviderAssessment> ProviderAssessments => Set<ProviderAssessment>();
    public DbSet<ProviderWallet> ProviderWallets => Set<ProviderWallet>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<ProviderServiceArea> ProviderServiceAreas => Set<ProviderServiceArea>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ProviderService> ProviderServices => Set<ProviderService>();
    public DbSet<ChecklistTemplate> ChecklistTemplates => Set<ChecklistTemplate>();
    public DbSet<ChecklistTemplateItem> ChecklistTemplateItems => Set<ChecklistTemplateItem>();
    public DbSet<AvailabilityWindow> AvailabilityWindows => Set<AvailabilityWindow>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingTask> BookingTasks => Set<BookingTask>();
    public DbSet<BookingStatusHistory> BookingStatusHistory => Set<BookingStatusHistory>();
    public DbSet<CompanyWorkerAssignment> CompanyWorkerAssignments => Set<CompanyWorkerAssignment>();
    public DbSet<CheckInToken> CheckInTokens => Set<CheckInToken>();
    public DbSet<BookingPhoto> BookingPhotos => Set<BookingPhoto>();
    public DbSet<BookingExtension> BookingExtensions => Set<BookingExtension>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MoneyMovement> MoneyMovements => Set<MoneyMovement>();
    public DbSet<PromotionPlan> PromotionPlans => Set<PromotionPlan>();
    public DbSet<PromotionPurchase> PromotionPurchases => Set<PromotionPurchase>();
    public DbSet<ProviderMetricDaily> ProviderMetricsDaily => Set<ProviderMetricDaily>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeEvidence> DisputeEvidence => Set<DisputeEvidence>();
    public DbSet<DisputeDecision> DisputeDecisions => Set<DisputeDecision>();
    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyAbstrictConventions();
    }
}
