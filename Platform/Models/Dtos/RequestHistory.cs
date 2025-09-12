using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class RequestHistory
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RequestId { get; set; }

    public int? OldStatusId { get; set; }

    [Required]
    public int NewStatusId { get; set; }

    [Required]
    public string ChangedByUserId { get; set; }

    [Required]
    public DateTime ChangeDate { get; set; } = DateTime.UtcNow;

    public string? Comment { get; set; }

    [StringLength(100)]
    public string? FieldChanged { get; set; }

    // Навигационные свойства
    [ForeignKey("RequestId")]
    public Request Request { get; set; }

    [ForeignKey("OldStatusId")]
    public RequestStatus? OldStatus { get; set; }

    [ForeignKey("NewStatusId")]
    public RequestStatus NewStatus { get; set; }

    [ForeignKey("ChangedByUserId")]
    public ApplicationUser ChangedByUser { get; set; }
}
