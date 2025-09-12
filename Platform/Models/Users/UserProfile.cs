using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Models.Users;

public class UserProfile
{
    [Key]
    [ForeignKey("User")]
    public string UserId { get; set; }

    [StringLength(100)]
    public string? Department { get; set; }

    public string? Qualification { get; set; }

    [Required]
    public int Workload { get; set; } = 0;

    // Навигационные свойства
    public ApplicationUser User { get; set; }
}
