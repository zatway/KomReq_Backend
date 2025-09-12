using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class Report
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string GeneratedByUserId { get; set; }

    [Required]
    [StringLength(50)]
    public string ReportType { get; set; }

    public string? Parameters { get; set; } // JSONB в базе

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; }

    [Required]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    [ForeignKey("GeneratedByUserId")]
    public ApplicationUser GeneratedByUser { get; set; }
}
