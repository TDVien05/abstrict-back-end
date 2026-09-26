namespace Abstrict.Api.Models.Entities;

public sealed class ChecklistTemplateItem : Entity
{
    public Guid ChecklistTemplateId { get; set; }
    public Guid? ParentItemId { get; set; }
    public required string AreaName { get; set; }
    public required string Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public ChecklistTemplate ChecklistTemplate { get; set; } = null!;
    public ChecklistTemplateItem? ParentItem { get; set; }
}
