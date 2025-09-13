using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Platform.Models.Response.Request;

public class UploadFileDto
{
    [Required]
    public IFormFile File { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsConfidential { get; set; }
}