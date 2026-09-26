using Abstrict.Api.Data;
using Abstrict.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Tests.Architecture;

public sealed class ProjectStructureTests
{
    [Fact]
    public void ApiAssembly_CanBeLoaded()
    {
        var assembly = typeof(Program).Assembly;

        Assert.Equal("Abstrict.Api", assembly.GetName().Name);
    }

    [Fact]
    public void EfCoreModel_ContainsCoreScreenEntities()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=abstrict;Username=test;Password=test")
            .Options;

        using var context = new AppDbContext(options);
        var model = context.Model;

        Assert.NotNull(model.FindEntityType(typeof(Provider)));
        Assert.NotNull(model.FindEntityType(typeof(Booking)));
        Assert.NotNull(model.FindEntityType(typeof(Payment)));
        Assert.NotNull(model.FindEntityType(typeof(PromotionPurchase)));
        Assert.NotNull(model.FindEntityType(typeof(Dispute)));
        Assert.NotNull(model.FindEntityType(typeof(ProviderApprovalReview)));
        Assert.NotNull(model.FindEntityType(typeof(ProviderAssessment)));
        Assert.NotNull(model.FindEntityType(typeof(ProviderWallet)));
        Assert.NotNull(model.FindEntityType(typeof(DisputeMessage)));
        Assert.NotNull(model.FindEntityType(typeof(AuditLog)));
        Assert.True(model.GetEntityTypes().Count() >= 39);
    }
}
