using Abstrict.Api.DTOs.Responses;

namespace Abstrict.Api.Services.Interfaces;

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogServiceAreaResponse>> GetServiceAreasAsync(string? city, CancellationToken cancellationToken);
    /// <summary>Tỉnh/thành kèm quận/huyện theo dữ liệu hành chính Việt Nam, lọc theo tên tỉnh/thành nếu có <paramref name="city"/>.</summary>
    Task<IReadOnlyList<AdministrativeProvinceResponse>> GetAdministrativeDivisionsAsync(string? city, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogServiceCategoryResponse>> GetServiceCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogBankResponse>> GetBanksAsync(CancellationToken cancellationToken);

    /// <summary>Template checklist đang hiệu lực (version cao nhất) của một danh mục; <c>null</c> nếu chưa có.</summary>
    Task<ChecklistTemplateResponse?> GetChecklistAsync(string serviceCategoryCode, CancellationToken cancellationToken);
}
