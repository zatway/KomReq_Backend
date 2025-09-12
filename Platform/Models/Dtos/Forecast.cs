using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Dtos;

public class Forecast
{
    [Key]
    public int Id { get; set; }

    [Required]
    public DateTime PeriodStart { get; set; }

    [Required]
    public DateTime PeriodEnd { get; set; }

    [Required]
    public int PredictedRequests { get; set; }

    [Required]
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}