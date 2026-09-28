using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Models.Enums;
using Abstrict.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Repositories.Implementations;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new UnitOfWorkTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class UnitOfWorkTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

public sealed class FreelancerApplicationRepository(AppDbContext dbContext) : IFreelancerApplicationRepository
{
    public Task<FreelancerApplication?> GetByUserIdAsync(Guid userId, bool track, CancellationToken cancellationToken) =>
        Query(track).SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public Task<FreelancerApplication?> GetByIdAsync(Guid id, bool track, CancellationToken cancellationToken) =>
        Query(track).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<FreelancerApplication?> GetByProviderIdAsync(Guid providerId, bool track, CancellationToken cancellationToken) =>
        Query(track).SingleOrDefaultAsync(x => x.ProviderId == providerId, cancellationToken);

    public async Task AddAsync(FreelancerApplication application, CancellationToken cancellationToken) =>
        await dbContext.FreelancerApplications.AddAsync(application, cancellationToken);

    public async Task<IReadOnlyList<FreelancerApplication>> ListByStatusAsync(string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.FreelancerApplications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FreelancerApplicationStatus>(status, true, out var parsed))
            query = query.Where(x => x.Status == parsed);

        return await query.OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountByStatusAsync(string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.FreelancerApplications.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FreelancerApplicationStatus>(status, true, out var parsed))
            query = query.Where(x => x.Status == parsed);

        return await query.LongCountAsync(cancellationToken);
    }

    private IQueryable<FreelancerApplication> Query(bool track) =>
        track ? dbContext.FreelancerApplications : dbContext.FreelancerApplications.AsNoTracking();
}

public sealed class VerificationDocumentRepository(AppDbContext dbContext) : IVerificationDocumentRepository
{
    public Task<VerificationDocument?> GetAsync(Guid id, bool track, CancellationToken cancellationToken) =>
        Query(track).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<VerificationDocument?> GetByObjectKeyAsync(string objectKey, bool track, CancellationToken cancellationToken) =>
        Query(track).SingleOrDefaultAsync(x => x.ObjectKey == objectKey, cancellationToken);

    public Task<VerificationDocument?> GetCurrentAsync(Guid applicationId, VerificationDocumentType type, CancellationToken cancellationToken) =>
        dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId && x.Type == type && x.SupersededAtUtc == null)
            .OrderByDescending(x => x.Revision)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<VerificationDocument>> ListByApplicationAsync(Guid applicationId, bool track, CancellationToken cancellationToken) =>
        await Query(track).Where(x => x.ApplicationId == applicationId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<VerificationDocument>> ListCurrentByApplicationAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await dbContext.VerificationDocuments.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId && x.SupersededAtUtc == null)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(VerificationDocument document, CancellationToken cancellationToken) =>
        await dbContext.VerificationDocuments.AddAsync(document, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<VerificationDocument> documents, CancellationToken cancellationToken) =>
        await dbContext.VerificationDocuments.AddRangeAsync(documents, cancellationToken);

    private IQueryable<VerificationDocument> Query(bool track) =>
        track ? dbContext.VerificationDocuments : dbContext.VerificationDocuments.AsNoTracking();
}

public sealed class KycOperationRepository(AppDbContext dbContext) : IKycOperationRepository
{
    public Task<KycOperation?> GetAsync(Guid id, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.KycOperations : dbContext.KycOperations.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<KycOperation?> FindByIdempotencyKeyAsync(Guid userId, KycOperationType type, string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.KycOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Type == type && x.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(KycOperation operation, CancellationToken cancellationToken) =>
        await dbContext.KycOperations.AddAsync(operation, cancellationToken);

    public async Task<KycOperation?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var operation = await dbContext.KycOperations
            .FromSqlInterpolated($"""
                SELECT * FROM kyc_operations
                WHERE state = 'Queued'
                  AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {now})
                  AND (lease_expires_at_utc IS NULL OR lease_expires_at_utc <= {now})
                ORDER BY created_at_utc
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (operation is null)
            return null;

        operation.State = KycOperationState.Processing;
        operation.LeaseExpiresAtUtc = now.AddMinutes(5);
        operation.AttemptCount++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return operation;
    }
}

public sealed class ApplicationSubmissionRepository(AppDbContext dbContext) : IApplicationSubmissionRepository
{
    public Task<ApplicationSubmission?> GetLatestAsync(Guid applicationId, CancellationToken cancellationToken) =>
        dbContext.ApplicationSubmissions.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ApplicationSubmission?> GetByVersionAsync(Guid applicationId, int version, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.ApplicationSubmissions : dbContext.ApplicationSubmissions.AsNoTracking())
            .SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.Version == version, cancellationToken);

    public async Task AddAsync(ApplicationSubmission submission, CancellationToken cancellationToken) =>
        await dbContext.ApplicationSubmissions.AddAsync(submission, cancellationToken);
}

public sealed class IdentityClaimRepository(AppDbContext dbContext) : IIdentityClaimRepository
{
    public Task<IdentityClaim?> GetByFingerprintAsync(string fingerprint, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.IdentityClaims : dbContext.IdentityClaims.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Fingerprint == fingerprint, cancellationToken);

    public async Task AddAsync(IdentityClaim claim, CancellationToken cancellationToken) =>
        await dbContext.IdentityClaims.AddAsync(claim, cancellationToken);
}

public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByPhoneNumberAsync(string phoneNumber, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.Users : dbContext.Users.AsNoTracking())
            .SingleOrDefaultAsync(x => x.PhoneNumber == phoneNumber, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.Users : dbContext.Users.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken) =>
        await dbContext.Users.AddAsync(user, cancellationToken);
}

public sealed class IdentityAttemptRepository(AppDbContext dbContext) : IIdentityAttemptRepository
{
    public Task<IdentityVerificationAttempt?> GetAsync(Guid id, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.IdentityVerificationAttempts : dbContext.IdentityVerificationAttempts.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<IdentityVerificationAttempt?> GetCurrentAsync(Guid applicationId, bool track, CancellationToken cancellationToken) =>
        (track ? dbContext.IdentityVerificationAttempts : dbContext.IdentityVerificationAttempts.AsNoTracking())
            .Where(x => x.ApplicationId == applicationId && x.SupersededAtUtc == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(IdentityVerificationAttempt attempt, CancellationToken cancellationToken) =>
        await dbContext.IdentityVerificationAttempts.AddAsync(attempt, cancellationToken);
}

public sealed class KycConsentRepository(AppDbContext dbContext) : IKycConsentRepository
{
    public Task<KycConsent?> GetActiveAsync(Guid userId, KycConsentType type, CancellationToken cancellationToken) =>
        dbContext.KycConsents.AsNoTracking()
            .Where(x => x.UserId == userId && x.ConsentType == type && x.WithdrawnAtUtc == null)
            .OrderByDescending(x => x.AcceptedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(KycConsent consent, CancellationToken cancellationToken) =>
        await dbContext.KycConsents.AddAsync(consent, cancellationToken);
}
