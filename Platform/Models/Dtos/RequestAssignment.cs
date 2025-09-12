using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Enums;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class RequestAssignment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RequestId { get; set; }

    [Required]
    public string UserId { get; set; }

    [Required]
    public RequestAssignmentRole RoleInRequest { get; set; }

    [Required]
    public DateTime AssignedDate { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedDate { get; set; }

    // Навигационные свойства
    [ForeignKey("RequestId")]
    public Request Request { get; set; }

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; }
}