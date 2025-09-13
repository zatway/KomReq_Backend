using System.ComponentModel.DataAnnotations;
using Platform.Models.Enums;

namespace Platform.Models.Response.Request;

public class AssignUserDto
{
    [Required]
    public string UserId { get; set; }

    [Required]
    public RequestAssignmentRole Role { get; set; }
}