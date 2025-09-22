using System.ComponentModel.DataAnnotations;

namespace Platform.Models.Request.Request;

public class AddCommentDto
{
    [Required]
    [StringLength(1000)]
    public string Comment { get; set; } = string.Empty;
}

