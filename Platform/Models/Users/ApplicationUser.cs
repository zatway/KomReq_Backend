using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Platform.Models.Dtos;

namespace Platform.Models.Users
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(200)]
        public string FullName { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public UserProfile? UserProfile { get; set; }
        public ICollection<Dtos.Request> ManagedRequests { get; set; } = new List<Dtos.Request>();
        public ICollection<RequestAssignment> RequestAssignments { get; set; } = new List<RequestAssignment>();
        public ICollection<RequestHistory> RequestHistories { get; set; } = new List<RequestHistory>();
        public ICollection<RequestFile> UploadedFiles { get; set; } = new List<RequestFile>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
        public ICollection<Report> GeneratedReports { get; set; } = new List<Report>();
    }
}