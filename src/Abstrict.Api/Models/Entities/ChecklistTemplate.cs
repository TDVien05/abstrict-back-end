namespace Abstrict.Api.Models.Entities;

public sealed class ChecklistTemplate : Entity
{
    public Guid ServiceCategoryId { get; set; }
    public required string Name { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public ServiceCategory ServiceCategory { get; set; } = null!;
    public ICollection<ChecklistTemplateItem> Items { get; set; } = new List<ChecklistTemplateItem>();
}
