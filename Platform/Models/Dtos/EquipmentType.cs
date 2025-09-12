using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Models.Dtos;

public class EquipmentType
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; }

    public string? Description { get; set; }

    public string? Specifications { get; set; } // JSONB в базе (строка в C#)

    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; } = 0;

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ICollection<Request> Requests { get; set; } = new List<Request>();
}
