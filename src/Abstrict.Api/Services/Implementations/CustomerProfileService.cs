using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class CustomerProfileService(AppDbContext dbContext) : ICustomerProfileService
{
    public async Task<CustomerProfileResponse> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().Include(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user?.CustomerProfile is not { } profile)
            throw new BookingFlowException(StatusCodes.Status404NotFound, "CUSTOMER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ khách hàng.");

        var addresses = await CustomerAddressService.LoadAsync(dbContext, userId, cancellationToken);
        return CustomerAvatarService.ToProfileResponse(user, profile, addresses);
    }
}
