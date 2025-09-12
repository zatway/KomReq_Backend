using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Models.Enums;
using Platform.Models.Users;

namespace Platform.Models.Dtos;

public class Request
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [Required]
    public int EquipmentTypeId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    public RequestPriority Priority { get; set; } = RequestPriority.Medium;

    [Required]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? TargetCompletion { get; set; }

    public string? ManagerId { get; set; }

    [Required]
    public int CurrentStatusId { get; set; } = 1; // Новая по умолчанию

    public string? Comments { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    // Навигационные свойства
    [ForeignKey("ClientId")]
    public Client Client { get; set; }

    [ForeignKey("EquipmentTypeId")]
    public EquipmentType EquipmentType { get; set; }

    [ForeignKey("ManagerId")]
    public ApplicationUser? Manager { get; set; }

    [ForeignKey("CurrentStatusId")]
    public RequestStatus CurrentStatus { get; set; }

    public ICollection<RequestAssignment> RequestAssignments { get; set; } = new List<RequestAssignment>();
    public ICollection<RequestHistory> RequestHistories { get; set; } = new List<RequestHistory>();
    public ICollection<RequestFile> RequestFiles { get; set; } = new List<RequestFile>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}