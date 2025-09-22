using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Request.EquipmentType;

public class CreateEquipmentTypeDto
{
    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Specifications { get; set; }

    [Required]
    public decimal Price { get; set; } = 0;

    public bool IsActive { get; set; } = true;
}

