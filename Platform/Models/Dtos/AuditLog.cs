using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class AuditLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    [StringLength(100)]
    public string Action { get; set; }

    public int? EntityId { get; set; }

    [StringLength(50)]
    public string? EntityType { get; set; }

    public string? Details { get; set; } // JSONB в базе

    public string? IpAddress { get; set; }

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; }
}