using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platform.Models;
using Platform.Models.Dtos;
using Platform.Models.Roles;
using Platform.Models.Users;

namespace Infrastructure.DbContext
{
    public class KomReqDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public KomReqDbContext(DbContextOptions<KomReqDbContext> options) : base(options)
        {
        }

        public DbSet<Client> Clients { get; set; }
        public DbSet<EquipmentType> EquipmentTypes { get; set; }
        public DbSet<RequestStatus> RequestStatuses { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<RequestAssignment> RequestAssignments { get; set; }
        public DbSet<RequestHistory> RequestHistories { get; set; }
        public DbSet<RequestFile> RequestFiles { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<StatusStatistic> StatusStatistics { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Forecast> Forecasts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // ApplicationUser
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.UserProfile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Client
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.Email)
                .IsUnique();
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.Tin)
                .IsUnique();
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.UniqueCode)
                .IsUnique();
            modelBuilder.Entity<Client>()
                .HasIndex(c => c.CompanyName);

            // EquipmentType
            modelBuilder.Entity<EquipmentType>()
                .HasIndex(e => e.Name)
                .IsUnique();
            modelBuilder.Entity<EquipmentType>()
                .Property(e => e.Specifications)
                .HasColumnType("jsonb"); // Для PostgreSQL

            // RequestStatus
            modelBuilder.Entity<RequestStatus>()
                .HasIndex(rs => rs.Name)
                .IsUnique();
            modelBuilder.Entity<RequestStatus>()
                .HasIndex(rs => rs.OrderNum)
                .IsUnique();
            // Начальные данные для статусов
            modelBuilder.Entity<RequestStatus>().HasData(
                new RequestStatus { Id = 1, Name = "Новая", OrderNum = 1, IsFinal = false },
                new RequestStatus { Id = 2, Name = "В обработке", OrderNum = 2, IsFinal = false },
                new RequestStatus { Id = 3, Name = "В работе", OrderNum = 3, IsFinal = false },
                new RequestStatus { Id = 4, Name = "Наладка", OrderNum = 4, IsFinal = false },
                new RequestStatus { Id = 5, Name = "Завершена", OrderNum = 5, IsFinal = true },
                new RequestStatus { Id = 6, Name = "Отменена", OrderNum = 6, IsFinal = true }
            );

            // Request
            modelBuilder.Entity<Request>()
                .HasIndex(r => r.ClientId);
            modelBuilder.Entity<Request>()
                .HasIndex(r => r.EquipmentTypeId);
            modelBuilder.Entity<Request>()
                .HasIndex(r => r.CurrentStatusId);
            modelBuilder.Entity<Request>()
                .HasIndex(r => r.CreatedDate);
            modelBuilder.Entity<Request>()
                .HasOne(r => r.Client)
                .WithMany(c => c.Requests)
                .HasForeignKey(r => r.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Request>()
                .HasOne(r => r.EquipmentType)
                .WithMany(e => e.Requests)
                .HasForeignKey(r => r.EquipmentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Request>()
                .HasOne(r => r.Manager)
                .WithMany(u => u.ManagedRequests)
                .HasForeignKey(r => r.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Request>()
                .HasOne(r => r.CurrentStatus)
                .WithMany(s => s.Requests)
                .HasForeignKey(r => r.CurrentStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            // RequestAssignment
            modelBuilder.Entity<RequestAssignment>()
                .HasIndex(ra => new { ra.RequestId, ra.UserId, ra.RoleInRequest })
                .IsUnique();
            modelBuilder.Entity<RequestAssignment>()
                .HasIndex(ra => ra.UserId);
            modelBuilder.Entity<RequestAssignment>()
                .HasOne(ra => ra.Request)
                .WithMany(r => r.RequestAssignments)
                .HasForeignKey(ra => ra.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RequestAssignment>()
                .HasOne(ra => ra.User)
                .WithMany(u => u.RequestAssignments)
                .HasForeignKey(ra => ra.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // RequestHistory
            modelBuilder.Entity<RequestHistory>()
                .HasIndex(rh => rh.RequestId);
            modelBuilder.Entity<RequestHistory>()
                .HasIndex(rh => rh.ChangeDate);
            modelBuilder.Entity<RequestHistory>()
                .HasOne(rh => rh.Request)
                .WithMany(r => r.RequestHistories)
                .HasForeignKey(rh => rh.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RequestHistory>()
                .HasOne(rh => rh.OldStatus)
                .WithMany(s => s.OldStatusHistories)
                .HasForeignKey(rh => rh.OldStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RequestHistory>()
                .HasOne(rh => rh.NewStatus)
                .WithMany(s => s.NewStatusHistories)
                .HasForeignKey(rh => rh.NewStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<RequestHistory>()
                .HasOne(rh => rh.ChangedByUser)
                .WithMany(u => u.RequestHistories)
                .HasForeignKey(rh => rh.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // RequestFile
            modelBuilder.Entity<RequestFile>()
                .HasIndex(rf => rf.RequestId);
            modelBuilder.Entity<RequestFile>()
                .HasIndex(rf => rf.UploadedDate);
            modelBuilder.Entity<RequestFile>()
                .HasOne(rf => rf.Request)
                .WithMany(r => r.RequestFiles)
                .HasForeignKey(rf => rf.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<RequestFile>()
                .HasOne(rf => rf.UploadedByUser)
                .WithMany(u => u.UploadedFiles)
                .HasForeignKey(rf => rf.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.UserId);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.ClientId);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.SentDate);
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Request)
                .WithMany(r => r.Notifications)
                .HasForeignKey(n => n.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Client)
                .WithMany(c => c.Notifications)
                .HasForeignKey(n => n.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // AuditLog
            modelBuilder.Entity<AuditLog>()
                .HasIndex(al => al.UserId);
            modelBuilder.Entity<AuditLog>()
                .HasIndex(al => al.Timestamp);
            modelBuilder.Entity<AuditLog>()
                .Property(al => al.Details)
                .HasColumnType("jsonb"); // Для PostgreSQL
            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // UserProfile
            modelBuilder.Entity<UserProfile>()
                .HasIndex(up => up.Department);

            // StatusStatistic
            modelBuilder.Entity<StatusStatistic>()
                .HasIndex(ss => ss.StatusId);
            modelBuilder.Entity<StatusStatistic>()
                .HasIndex(ss => ss.Date);
            modelBuilder.Entity<StatusStatistic>()
                .HasOne(ss => ss.Status)
                .WithMany(s => s.StatusStatistics)
                .HasForeignKey(ss => ss.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            // Report
            modelBuilder.Entity<Report>()
                .HasIndex(r => r.GeneratedByUserId);
            modelBuilder.Entity<Report>()
                .HasIndex(r => r.GeneratedAt);
            modelBuilder.Entity<Report>()
                .Property(r => r.Parameters)
                .HasColumnType("jsonb"); // Для PostgreSQL
            modelBuilder.Entity<Report>()
                .HasOne(r => r.GeneratedByUser)
                .WithMany(u => u.GeneratedReports)
                .HasForeignKey(r => r.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Forecast
            modelBuilder.Entity<Forecast>()
                .HasIndex(f => new { f.PeriodStart, f.PeriodEnd });
        }
    }
}