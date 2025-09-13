using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Platform.Helpers.UsersHelper;
using Platform.Models.Users;
using Platform.Models.Roles;

namespace Infrastructure.DbContext;

public class DatabaseInitializer
{
    private readonly KomReqDbContext _db;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(
        KomReqDbContext db,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _db = db;
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        await EnsureDatabaseExistsAsync();
        await _db.Database.MigrateAsync();
        await SeedRolesAsync();
        await SeedAdminAsync();
    }

    private async Task EnsureDatabaseExistsAsync()
    {
        var builderCs = new NpgsqlConnectionStringBuilder(_db.Database.GetDbConnection().ConnectionString);
        var databaseName = builderCs.Database;

        var postgresConnectionString = new NpgsqlConnectionStringBuilder(_db.Database.GetDbConnection().ConnectionString)
        {
            Database = "postgres",
            Pooling = false
        }.ConnectionString;

        await using var postgresConn = new NpgsqlConnection(postgresConnectionString);
        await postgresConn.OpenAsync();

        await using var checkCmd = new NpgsqlCommand($"SELECT 1 FROM pg_catalog.pg_database WHERE datname = @dbName", postgresConn);
        checkCmd.Parameters.AddWithValue("dbName", databaseName);
        var exists = await checkCmd.ExecuteScalarAsync() != null;

        if (!exists)
        {
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", postgresConn);
            await createCmd.ExecuteNonQueryAsync();
            Console.WriteLine($"База данных '{databaseName}' создана.");
        }
        else
        {
            Console.WriteLine($"База данных '{databaseName}' уже существует.");
        }
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = new[] { "Admin", "Manager", "Technic", "Client" };
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole
                {
                    Name = role,
                    Description = AddUserService.GetDescriptionByRoleName(role)
                });
                Console.WriteLine($"Роль '{role}' создана.");
            }
        }
    }

    private async Task SeedAdminAsync()
    {
        var adminEmail = _configuration["DefaultAdmin:Email"] ?? "admin@system.local";
        var adminUserName = _configuration["DefaultAdmin:UserName"] ?? "admin";
        var adminPassword = _configuration["DefaultAdmin:Password"] ?? "Admin123!";

        var adminUser = await _userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, adminPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Admin");
                Console.WriteLine("Учётка администратора создана.");
            }
            else
            {
                Console.WriteLine("Ошибка при создании администратора: " + string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            Console.WriteLine("Учётка администратора уже существует.");
        }
    }
}
