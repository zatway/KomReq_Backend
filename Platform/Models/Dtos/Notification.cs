using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Enums;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class Notification
{
    [Key]
    public int Id { get; set; }

    public int? RequestId { get; set; }

    public string? UserId { get; set; }

    // public int? ClientId { get; set; }

    [Required]
    public NotificationType Type { get; set; }

    [Required]
    public string Message { get; set; }

    [Required]
    public DateTime SentDate { get; set; } = DateTime.UtcNow;

    [Required]
    public bool IsRead { get; set; } = false;

    [Required]
    public NotificationDeliveryStatus DeliveryStatus { get; set; } = NotificationDeliveryStatus.Pending;

    // Навигационные свойства
    [ForeignKey("RequestId")]
    public Request? Request { get; set; }

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }

    // [ForeignKey("ClientId")]
    // public Client? Client { get; set; }
}
