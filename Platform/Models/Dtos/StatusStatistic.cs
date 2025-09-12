
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Models.Dtos;
public class StatusStatistic
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int StatusId { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [Required]
    public int CountRequests { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? AvgCompletionDays { get; set; }

    // Навигационные свойства
    [ForeignKey("StatusId")]
    public RequestStatus Status { get; set; }
}