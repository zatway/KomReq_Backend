using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Dtos;

public class RequestStatus
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; }

    public string? Description { get; set; }

    [Required]
    public bool IsFinal { get; set; } = false;

    [Required]
    public int OrderNum { get; set; }

    // Навигационные свойства
    public ICollection<Request> Requests { get; set; } = new List<Request>();
    public ICollection<RequestHistory> OldStatusHistories { get; set; } = new List<RequestHistory>();
    public ICollection<RequestHistory> NewStatusHistories { get; set; } = new List<RequestHistory>();
    public ICollection<StatusStatistic> StatusStatistics { get; set; } = new List<StatusStatistic>();
}