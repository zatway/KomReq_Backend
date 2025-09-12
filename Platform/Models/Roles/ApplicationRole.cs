using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Platform.Models.Roles
{
    public class ApplicationRole : IdentityRole
    {
        
        [Required]
        [StringLength(50)]
        public string Description { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}