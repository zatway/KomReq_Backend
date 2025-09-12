using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Dtos;

public class Client
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string? CompanyName { get; set; }

    [Required]
    [StringLength(200)]
    public string FullName { get; set; }

    [Required]
    [StringLength(255)]
    public string Email { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    public string? Address { get; set; }

    [StringLength(20)]
    public string? Tin { get; set; }

    [Required]
    public Guid UniqueCode { get; set; } = Guid.NewGuid();

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Навигационные свойства
    public ICollection<Request> Requests { get; set; } = new List<Request>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
