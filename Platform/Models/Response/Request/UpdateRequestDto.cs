using System.ComponentModel.DataAnnotations;
using Platform.Models.Enums;

namespace Platform.Models.Response.Request;

public class UpdateRequestDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    public RequestPriority Priority { get; set; }

    public DateTime? TargetCompletion { get; set; }

    [StringLength(1000)]
    public string? Comments { get; set; }
}
