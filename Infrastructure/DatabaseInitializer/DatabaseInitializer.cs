using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Platform.Helpers.UsersHelper;
using Platform.Models.Users;
using Platform.Models.Roles;
using Platform.Models.Dtos;
using Platform.Models.Enums;

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
        await SeedDefaultUsersAsync();
        await SeedEquipmentTypesAsync();
        await SeedSampleRequestsAsync();
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
        string[] roles = new[] { "Admin", "Manager", "Technician", "Client" };
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
        var adminEmail = _configuration["DefaultAdmin:Email"] ?? "admin@mail.ru";
        var adminUserName = _configuration["DefaultAdmin:UserName"] ?? "admin";
        var adminPassword = _configuration["DefaultAdmin:Password"] ?? "Admin123!";

        var adminUser = await _userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            var user = new ApplicationUser
            {
                UserName = adminUserName,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Администратор" // или adminUserName, или что-то из конфигурации
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

    private async Task SeedDefaultUsersAsync()
    {
        await EnsureUserWithRoleAsync("manager@mail.ru", "manager", "Admin123!", new[] { "Manager" });
        await EnsureUserWithRoleAsync("technician@mail.ru", "technician", "Admin123!", new[] { "Technician" });
        await EnsureUserWithRoleAsync("client@mail.ru", "client", "Admin123!", new[] { "Client" });
        await EnsureUserWithRoleAsync("superuser@mail.ru", "superuser", "Admin123!", new[] { "Admin", "Manager", "Technician", "Client" });
    }

    private async Task EnsureUserWithRoleAsync(string email, string userName, string password, string[] roles)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            foreach (var role in roles)
            {
                if (!await _userManager.IsInRoleAsync(existing, role))
                    await _userManager.AddToRoleAsync(existing, role);
            }
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = userName
        };
        var result = await _userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            foreach (var role in roles)
                await _userManager.AddToRoleAsync(user, role);
        }
        else
        {
            Console.WriteLine($"Ошибка при создании пользователя {email}: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedEquipmentTypesAsync()
    {
        if (await _db.EquipmentTypes.AnyAsync()) return;

        var names = new List<string>
        {
            "Вибропреобразователь КД6407",
            "Вибропреобразователь КД618",
            "Вибропреобразователь КД619",
            "Вибропреобразователь КД650",
            "Вибропреобразователь КД2061",
            "3-х компонентный датчик вибрации КДМ-321",
            "Вибропреобразователь КДМ322",
            "Датчики ударных импульсов КД420",
            "САНПО",
            "КДР",
            "ВИБ-8",
            "ВИБ-4",
            "Техпрогноз 5",
            "Техпрогноз 6"
        };

        foreach (var n in names)
        {
            _db.EquipmentTypes.Add(new EquipmentType
            {
                Name = n,
                Description = null,
                Specifications = null,
                Price = 0m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedSampleRequestsAsync()
    {
        if (await _db.Requests.AnyAsync()) return;

        var manager = await _userManager.FindByEmailAsync("manager@mail.ru");
        var technician = await _userManager.FindByEmailAsync("technician@mail.ru");
        var client = await _userManager.FindByEmailAsync("client@mail.ru");
        if (manager == null || technician == null || client == null) return;

        var eq1 = await _db.EquipmentTypes.FirstOrDefaultAsync(e => e.Name == "Вибропреобразователь КД6407");
        var eq2 = await _db.EquipmentTypes.FirstOrDefaultAsync(e => e.Name == "САНПО");
        if (eq1 == null || eq2 == null) return;

        var req1 = new Request
        {
            CreatorId = client.Id,
            EquipmentTypeId = eq1.Id,
            Quantity = 4,
            Priority = RequestPriority.High,
            CreatedDate = DateTime.UtcNow,
            ManagerId = manager.Id,
            CurrentStatusId = 1,
            Comments = "Тестовая заявка: поставка вибропреобразователей"
        };
        _db.Requests.Add(req1);
        await _db.SaveChangesAsync();

        _db.RequestHistories.Add(new RequestHistory
        {
            RequestId = req1.Id,
            NewStatusId = 1,
            ChangedByUserId = manager.Id,
            Comment = "Заявка создана",
            ChangeDate = DateTime.UtcNow
        });
        _db.RequestAssignments.Add(new RequestAssignment
        {
            RequestId = req1.Id,
            UserId = technician.Id,
            RoleInRequest = RequestAssignmentRole.Technician,
            AssignedDate = DateTime.UtcNow
        });

        var req2 = new Request
        {
            CreatorId = client.Id,
            EquipmentTypeId = eq2.Id,
            Quantity = 1,
            Priority = RequestPriority.Medium,
            CreatedDate = DateTime.UtcNow.AddDays(-2),
            ManagerId = manager.Id,
            CurrentStatusId = 2,
            Comments = "Тестовая заявка: измерительный комплекс"
        };
        _db.Requests.Add(req2);
        await _db.SaveChangesAsync();

        _db.RequestHistories.AddRange(
            new RequestHistory
            {
                RequestId = req2.Id,
                NewStatusId = 1,
                ChangedByUserId = manager.Id,
                Comment = "Заявка создана",
                ChangeDate = DateTime.UtcNow.AddDays(-2)
            },
            new RequestHistory
            {
                RequestId = req2.Id,
                OldStatusId = 1,
                NewStatusId = 2,
                ChangedByUserId = manager.Id,
                Comment = "Переведена в обработку",
                ChangeDate = DateTime.UtcNow.AddDays(-1)
            }
        );

        _db.RequestAssignments.Add(new RequestAssignment
        {
            RequestId = req2.Id,
            UserId = technician.Id,
            RoleInRequest = RequestAssignmentRole.Technician,
            AssignedDate = DateTime.UtcNow.AddDays(-1)
        });

        await _db.SaveChangesAsync();
    }
}
