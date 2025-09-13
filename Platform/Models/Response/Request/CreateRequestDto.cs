using System.ComponentModel.DataAnnotations;
using Platform.Models.Enums;

namespace Platform.Models.Request.Request;

public class CreateRequestDto
{
    [Required] public int ClientId { get; set; }

    [Required] public int EquipmentTypeId { get; set; }

    [Required] [Range(1, int.MaxValue)] public int Quantity { get; set; }

    [Required] public RequestPriority Priority { get; set; }

    public DateTime? TargetCompletion { get; set; }

    [StringLength(1000)] public string? Comments { get; set; }
}