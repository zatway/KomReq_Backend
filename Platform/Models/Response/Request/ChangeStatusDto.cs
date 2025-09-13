using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Response.Request;


public class ChangeStatusDto
{
    [Required]
    public int NewStatusId { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}