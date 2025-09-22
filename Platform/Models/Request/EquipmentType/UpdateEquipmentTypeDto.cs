using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Request.EquipmentType;

public class UpdateEquipmentTypeDto
{
    [StringLength(150)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Specifications { get; set; }

    public decimal? Price { get; set; }

    public bool? IsActive { get; set; }
}

