using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class RequestFile
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RequestId { get; set; }

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; }

    [Required]
    [StringLength(255)]
    public string FileName { get; set; }

    [StringLength(50)]
    public string? FileType { get; set; }

    public string? Description { get; set; }

    [Required]
    public string UploadedByUserId { get; set; }

    [Required]
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;

    [Required]
    public bool IsConfidential { get; set; } = false;

    // Навигационные свойства
    [ForeignKey("RequestId")]
    public Request Request { get; set; }

    [ForeignKey("UploadedByUserId")]
    public ApplicationUser UploadedByUser { get; set; }
}