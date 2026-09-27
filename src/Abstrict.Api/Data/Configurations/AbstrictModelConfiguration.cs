using System.Text.RegularExpressions;
using Abstrict.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Data.Configurations;

public static partial class AbstrictModelConfiguration
{
    public static void ApplyAbstrictConventions(this ModelBuilder modelBuilder)
    {
        ConfigureIdentity(modelBuilder);
        ConfigureProviders(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureBookings(modelBuilder);
        ConfigurePaymentsAndPromotions(modelBuilder);
        ConfigureTrust(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
    }

    private static void ConfigureIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(254);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<PhoneOtpChallenge>(entity =>
        {
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.CodeHash).HasMaxLength(500);
            entity.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.DeliveryChannel).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => new { x.UserId, x.Purpose, x.ConsumedAtUtc, x.ExpiresAtUtc, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.PhoneNumber, x.Purpose, x.CreatedAtUtc });
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_phone_otp_challenge_failed_attempt_count",
                "failed_attempt_count >= 0"));
            entity.HasOne(x => x.User).WithMany(x => x.PhoneOtpChallenges)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.Property(x => x.RefreshTokenHash).HasMaxLength(500);
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.RevokedAtUtc, x.ExpiresAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.AuthSessions)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.FullName).HasMaxLength(160);
            entity.HasOne(x => x.User).WithOne(x => x.CustomerProfile)
                .HasForeignKey<CustomerProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.Property(x => x.Label).HasMaxLength(80);
            entity.Property(x => x.ApartmentNumber).HasMaxLength(80);
            entity.Property(x => x.BuildingName).HasMaxLength(160);
            entity.Property(x => x.Ward).HasMaxLength(120);
            entity.Property(x => x.District).HasMaxLength(120);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.HasOne(x => x.CustomerUser).WithMany(x => x.CustomerAddresses)
                .HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProviders(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.ApprovalStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
            entity.ToTable(table => table.HasCheckConstraint("ck_provider_rating", "average_rating >= 0 AND average_rating <= 5"));
            entity.HasIndex(x => new { x.Type, x.ApprovalStatus, x.IsAcceptingBookings });
        });

        modelBuilder.Entity<FreelancerProfile>(entity =>
        {
            entity.HasIndex(x => x.ProviderId).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Property(x => x.Gender).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.FaceMatchScore).HasPrecision(5, 2);
            entity.HasOne(x => x.Provider).WithOne(x => x.FreelancerProfile)
                .HasForeignKey<FreelancerProfile>(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanyProfile>(entity =>
        {
            entity.HasIndex(x => x.ProviderId).IsUnique();
            entity.HasIndex(x => x.OwnerUserId).IsUnique();
            entity.HasIndex(x => x.TaxCode).IsUnique();
            entity.Property(x => x.WorkforceSize).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Provider).WithOne(x => x.CompanyProfile)
                .HasForeignKey<CompanyProfile>(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CompanyRepresentative>(entity =>
        {
            entity.HasIndex(x => x.CompanyProfileId).IsUnique();
            entity.HasOne(x => x.CompanyProfile).WithOne(x => x.Representative)
                .HasForeignKey<CompanyRepresentative>(x => x.CompanyProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompanyWorker>(entity =>
        {
            entity.HasIndex(x => new { x.CompanyProfileId, x.EmployeeCode }).IsUnique();
            entity.HasOne(x => x.CompanyProfile).WithMany(x => x.Workers)
                .HasForeignKey(x => x.CompanyProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VerificationDocument>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => new { x.ProviderId, x.Type });
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.HasIndex(x => new { x.ProviderId, x.IsDefault });
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProviderApprovalReview>(entity =>
        {
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ProviderId, x.Decision, x.SubmittedAtUtc });
            entity.HasIndex(x => new { x.AssignedAdminUserId, x.Decision });
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProviderAssessment>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.ToTable(table => table.HasCheckConstraint("ck_provider_assessment_score", "score >= 0 AND maximum_score > 0 AND score <= maximum_score AND passing_score >= 0 AND passing_score <= maximum_score"));
            entity.HasIndex(x => new { x.ProviderId, x.Type, x.CompletedAtUtc });
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProviderWallet>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.HasIndex(x => new { x.ProviderId, x.Type }).IsUnique();
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceArea>().HasIndex(x => new { x.City, x.District, x.WardOrComplex }).IsUnique();
        modelBuilder.Entity<ServiceCategory>().HasIndex(x => x.Code).IsUnique();

        modelBuilder.Entity<ProviderServiceArea>(entity =>
        {
            entity.HasIndex(x => new { x.ProviderId, x.ServiceAreaId }).IsUnique();
            entity.HasOne(x => x.Provider).WithMany(x => x.ServiceAreas).HasForeignKey(x => x.ProviderId);
            entity.HasOne(x => x.ServiceArea).WithMany().HasForeignKey(x => x.ServiceAreaId);
        });

        modelBuilder.Entity<ProviderService>(entity =>
        {
            entity.HasIndex(x => new { x.ProviderId, x.ServiceCategoryId }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("ck_provider_service_rate", "hourly_rate_vnd >= 0"));
            entity.HasOne(x => x.Provider).WithMany(x => x.Services).HasForeignKey(x => x.ProviderId);
            entity.HasOne(x => x.ServiceCategory).WithMany().HasForeignKey(x => x.ServiceCategoryId);
        });

        modelBuilder.Entity<ChecklistTemplate>(entity =>
        {
            entity.HasIndex(x => new { x.ServiceCategoryId, x.Version }).IsUnique();
            entity.HasOne(x => x.ServiceCategory).WithMany(x => x.ChecklistTemplates)
                .HasForeignKey(x => x.ServiceCategoryId);
        });

        modelBuilder.Entity<ChecklistTemplateItem>(entity =>
        {
            entity.HasOne(x => x.ChecklistTemplate).WithMany(x => x.Items).HasForeignKey(x => x.ChecklistTemplateId);
            entity.HasOne(x => x.ParentItem).WithMany().HasForeignKey(x => x.ParentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AvailabilityWindow>(entity =>
        {
            entity.HasIndex(x => new { x.ProviderId, x.StartsAtUtc, x.EndsAtUtc });
            entity.ToTable(table => table.HasCheckConstraint("ck_availability_range", "ends_at_utc > starts_at_utc"));
            entity.HasOne(x => x.Provider).WithMany(x => x.AvailabilityWindows)
                .HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBookings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasIndex(x => x.BookingNumber).IsUnique();
            entity.HasIndex(x => new { x.ProviderId, x.StartsAtUtc, x.EndsAtUtc });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_booking_range", "ends_at_utc > starts_at_utc");
                table.HasCheckConstraint("ck_booking_amounts", "unit_price_vnd >= 0 AND base_amount_vnd >= 0 AND surcharge_amount_vnd >= 0 AND deposit_amount_vnd >= 0 AND extension_amount_vnd >= 0");
            });
            entity.HasOne(x => x.CustomerUser).WithMany().HasForeignKey(x => x.CustomerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ServiceCategory).WithMany().HasForeignKey(x => x.ServiceCategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CustomerAddress).WithMany().HasForeignKey(x => x.CustomerAddressId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BookingTask>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Booking).WithMany(x => x.Tasks).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(40);
            entity.HasOne(x => x.Booking).WithMany(x => x.StatusHistory).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<CompanyWorkerAssignment>(entity =>
        {
            entity.HasIndex(x => new { x.BookingId, x.CompanyWorkerId, x.AssignedAtUtc });
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CompanyWorker).WithMany().HasForeignKey(x => x.CompanyWorkerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CheckInToken>(entity =>
        {
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.BookingId, x.ExpiresAtUtc });
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<BookingPhoto>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Booking).WithMany(x => x.Photos).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<BookingExtension>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.ToTable(table => table.HasCheckConstraint("ck_extension_values", "additional_minutes > 0 AND additional_amount_vnd >= 0"));
            entity.HasOne(x => x.Booking).WithMany(x => x.Extensions).HasForeignKey(x => x.BookingId);
        });
    }

    private static void ConfigurePaymentsAndPromotions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.ProviderName, x.ProviderTransactionId });
            entity.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(24);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.ToTable(table => table.HasCheckConstraint("ck_payment_amount", "amount_vnd > 0"));
            entity.HasOne(x => x.Booking).WithMany(x => x.Payments).HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PayerUser).WithMany().HasForeignKey(x => x.PayerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MoneyMovement>(entity =>
        {
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(24);
            entity.ToTable(table => table.HasCheckConstraint("ck_money_movement_amount", "amount_vnd > 0"));
            entity.HasOne(x => x.Payment).WithMany(x => x.MoneyMovements).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BeneficiaryProvider).WithMany().HasForeignKey(x => x.BeneficiaryProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProviderWallet).WithMany(x => x.MoneyMovements).HasForeignKey(x => x.ProviderWalletId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PromotionPlan>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Audience).HasConversion<string>().HasMaxLength(24);
            entity.ToTable(table => table.HasCheckConstraint("ck_promotion_plan_values", "duration_days > 0 AND price_vnd >= 0"));
        });

        modelBuilder.Entity<PromotionPurchase>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.ProviderId, x.Status, x.EndsAtUtc });
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PromotionPlan).WithMany().HasForeignKey(x => x.PromotionPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProviderMetricDaily>(entity =>
        {
            entity.HasIndex(x => new { x.ProviderId, x.MetricDate }).IsUnique();
            entity.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId);
        });
    }

    private static void ConfigureTrust(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasIndex(x => new { x.BookingId, x.ReviewerUserId }).IsUnique();
            entity.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(24);
            entity.ToTable(table => table.HasCheckConstraint("ck_rating_score", "score >= 1 AND score <= 5"));
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.BookingId, x.Status });
            entity.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DisputeEvidence>().HasOne(x => x.Dispute).WithMany(x => x.Evidence)
            .HasForeignKey(x => x.DisputeId);
        modelBuilder.Entity<DisputeDecision>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(24);
            entity.HasIndex(x => new { x.DisputeId, x.Type }).IsUnique();
            entity.HasOne(x => x.Dispute).WithMany(x => x.Decisions).HasForeignKey(x => x.DisputeId);
        });

        modelBuilder.Entity<DisputeMessage>(entity =>
        {
            entity.HasIndex(x => new { x.DisputeId, x.CreatedAtUtc });
            entity.HasOne(x => x.Dispute).WithMany(x => x.Messages).HasForeignKey(x => x.DisputeId);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.ReadAtUtc });
            entity.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.Action).HasMaxLength(120);
            entity.Property(x => x.EntityType).HasMaxLength(160);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.ActorUserId, x.CreatedAtUtc });
        });
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName() ?? entity.ClrType.Name));
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string name) =>
        SnakeCaseBoundary().Replace(name, "$1_$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex SnakeCaseBoundary();
}
