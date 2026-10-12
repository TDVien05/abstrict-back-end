using Abstrict.Api.Data;
using Abstrict.Api.DTOs.Requests;
using Abstrict.Api.DTOs.Responses;
using Abstrict.Api.Models.Entities;
using Abstrict.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abstrict.Api.Services.Implementations;

public sealed class CustomerAddressService(AppDbContext dbContext, TimeProvider timeProvider) : ICustomerAddressService
{
    public const int MaximumAddresses = 10;

    public async Task<IReadOnlyList<CustomerAddressResponse>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        var addresses = await dbContext.CustomerAddresses.AsNoTracking()
            .Where(x => x.CustomerUserId == userId)
            .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return addresses.Select(ToResponse).ToList();
    }

    public async Task<CustomerAddressResponse> CreateAsync(Guid userId, SaveCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.CustomerAddresses.Where(x => x.CustomerUserId == userId).ToListAsync(cancellationToken);
        if (existing.Count >= MaximumAddresses)
            throw new BookingFlowException(StatusCodes.Status409Conflict, "ADDRESS_LIMIT_REACHED",
                $"Mỗi tài khoản lưu tối đa {MaximumAddresses} địa chỉ.");

        var now = timeProvider.GetUtcNow();
        var address = new CustomerAddress
        {
            CustomerUserId = userId,
            Label = string.Empty,
            ApartmentNumber = string.Empty,
            BuildingName = string.Empty,
            StreetAddress = string.Empty,
            Ward = string.Empty,
            District = string.Empty,
            City = string.Empty,
            CreatedAtUtc = now
        };
        Apply(address, request, now);
        address.IsDefault = request.IsDefault || existing.Count == 0;
        if (address.IsDefault)
            ClearDefault(existing, now);

        dbContext.CustomerAddresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(address);
    }

    public async Task<CustomerAddressResponse> UpdateAsync(Guid userId, Guid addressId, SaveCustomerAddressRequest request, CancellationToken cancellationToken)
    {
        var addresses = await dbContext.CustomerAddresses.Where(x => x.CustomerUserId == userId).ToListAsync(cancellationToken);
        var address = addresses.SingleOrDefault(x => x.Id == addressId) ?? throw NotFound();

        var now = timeProvider.GetUtcNow();
        Apply(address, request, now);
        if (request.IsDefault)
        {
            ClearDefault(addresses.Where(x => x.Id != addressId), now);
            address.IsDefault = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(address);
    }

    public async Task DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        var addresses = await dbContext.CustomerAddresses.Where(x => x.CustomerUserId == userId).ToListAsync(cancellationToken);
        var address = addresses.SingleOrDefault(x => x.Id == addressId) ?? throw NotFound();

        dbContext.CustomerAddresses.Remove(address);
        if (address.IsDefault)
        {
            var next = addresses.Where(x => x.Id != addressId).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
            if (next is not null)
            {
                next.IsDefault = true;
                next.UpdatedAtUtc = timeProvider.GetUtcNow();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(CustomerAddress address, SaveCustomerAddressRequest request, DateTimeOffset now)
    {
        address.Label = request.Label.Trim();
        address.ApartmentNumber = request.ApartmentNumber.Trim();
        address.Floor = NullIfBlank(request.Floor);
        address.BuildingName = request.BuildingName.Trim();
        address.StreetAddress = request.StreetAddress.Trim();
        address.Ward = request.Ward.Trim();
        address.District = request.District.Trim();
        address.City = request.City.Trim();
        address.AccessInstructions = NullIfBlank(request.AccessInstructions);
        address.UpdatedAtUtc = now;
    }

    private static void ClearDefault(IEnumerable<CustomerAddress> addresses, DateTimeOffset now)
    {
        foreach (var other in addresses.Where(x => x.IsDefault))
        {
            other.IsDefault = false;
            other.UpdatedAtUtc = now;
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BookingFlowException NotFound() =>
        new(StatusCodes.Status404NotFound, "ADDRESS_NOT_FOUND", "Không tìm thấy địa chỉ.");

    private static CustomerAddressResponse ToResponse(CustomerAddress x) => new(
        x.Id, x.Label, x.ApartmentNumber, x.Floor, x.BuildingName, x.StreetAddress, x.Ward, x.District, x.City,
        x.AccessInstructions, x.IsDefault);
}
