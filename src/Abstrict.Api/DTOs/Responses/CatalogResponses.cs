namespace Abstrict.Api.DTOs.Responses;

public sealed record CatalogServiceAreaResponse(
    Guid Id,
    string City,
    string District,
    string? WardOrComplex,
    bool IsActive);

/// <summary>Quận/huyện/thị xã/thành phố thuộc tỉnh. <c>IsServed</c> cho biết nền tảng có khu vực dịch vụ đang hoạt động ở đây (đặt lịch được).</summary>
public sealed record AdministrativeDistrictResponse(int Code, string Name, bool IsServed);

/// <summary>Phường/xã thuộc một quận/huyện; <c>Name</c> là giá trị gửi lên <c>ward</c> khi lưu địa chỉ.</summary>
public sealed record AdministrativeWardResponse(string Code, string Name);

/// <summary><c>Name</c> bỏ tiền tố "Thành phố"/"Tỉnh" (ví dụ "Hồ Chí Minh") và là giá trị <c>city</c> dùng cho địa chỉ.</summary>
public sealed record AdministrativeProvinceResponse(int Code, string Name, IReadOnlyList<AdministrativeDistrictResponse> Districts);

public sealed record CatalogServiceCategoryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record CatalogBankResponse(
    string Code,
    string Name);
