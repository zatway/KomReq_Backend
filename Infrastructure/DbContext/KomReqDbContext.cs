using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platform.Models.Roles;
using Platform.Models.Users;

namespace Infrastructure.DbContext;

public class KomReqDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public KomReqDbContext(DbContextOptions<KomReqDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}