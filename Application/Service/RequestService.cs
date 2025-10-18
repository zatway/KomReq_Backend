using Infrastructure.DbContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Models.Dtos;
using Platform.Models.Enums;
using Platform.Models.Request.Request;
using Platform.Models.Response.Request;
using Platform.Models.Users;
using Microsoft.Extensions.Logging; // Add this using statement
using System.Text.Json;

namespace Application.Service;

public class RequestService
{
    private readonly KomReqDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RequestService> _logger; // Add this

    public RequestService(
        KomReqDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<RequestService> logger) // Add this
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger; // Assign the logger
    }

    public async Task<(bool Success, int RequestId, string ErrorMessage, int ErrorCode)> CreateRequestAsync(
        CreateRequestDto model, string currentUserId)
    {
        _logger.LogInformation("CreateRequestAsync called for UserId: {UserId}", currentUserId);

        // if (!await _dbContext.Clients.AnyAsync(c => c.Id == model.ClientId))
        //     return (false, 0, "Клиент не найден.", 404);

        if (!await _dbContext.EquipmentTypes.AnyAsync(e => e.Id == model.EquipmentTypeId && e.IsActive))
            return (false, 0, "Тип оборудования не найден или неактивен.", 404);

        var managerUser = await _userManager.FindByIdAsync(currentUserId); // Возвращено на FindByIdAsync
        if (managerUser == null)
        {
            _logger.LogWarning("Manager user not found for UserId: {UserId}", currentUserId); // Возвращено для лога
            return (false, 0, "Текущий пользователь не найден.", 404);
        }

        var roles = await _userManager.GetRolesAsync(managerUser);
        _logger.LogInformation("User {UserId} has roles: {Roles}", currentUserId, string.Join(", ", roles)); // Возвращено для лога

        if (!roles.Contains("Manager"))
        {
            _logger.LogWarning("User {UserId} is not a Manager. Roles: {Roles}", currentUserId, string.Join(", ", roles)); // Возвращено для лога
            return (false, 0, "Текущий пользователь не является действующим менеджером.", 403);
        }

        string creatorIdToUse = currentUserId;
        if (!string.IsNullOrEmpty(model.ClientUserId))
        {
            var clientUser = await _userManager.FindByIdAsync(model.ClientUserId);
            if (clientUser == null || !await _userManager.IsInRoleAsync(clientUser, "Client"))
                return (false, 0, "Указанный пользователь-клиент не найден или не имеет роли 'Client'.", 404);
            creatorIdToUse = model.ClientUserId;
        }

        var request = new Request
        {
            CreatorId = creatorIdToUse, // Используем ClientUserId или текущего пользователя
            EquipmentTypeId = model.EquipmentTypeId,
            Quantity = model.Quantity,
            Priority = Enum.Parse<RequestPriority>(model.Priority, ignoreCase: true),
            CreatedDate = DateTime.UtcNow,
            ManagerId = currentUserId, // Менеджер, создавший заявку
            CurrentStatusId = 1, // Новая
            Comments = model.Comments,
            TargetCompletion = model.TargetCompletion.HasValue
                ? DateTime.SpecifyKind(model.TargetCompletion.Value, DateTimeKind.Utc)
                : null,
        };

        _dbContext.Requests.Add(request);
        await _dbContext.SaveChangesAsync();

        var history = new RequestHistory
        {
            RequestId = request.Id,
            NewStatusId = 1,
            ChangedByUserId = currentUserId, // Пользователь, изменивший заявку
            Comment = "Заявка создана",
            ChangeDate = DateTime.UtcNow
        };
        _dbContext.RequestHistories.Add(history);

        var notification = new Notification
        {
            UserId = creatorIdToUse, // Уведомление теперь для создателя заявки (ClientUserId или текущий менеджер)
            RequestId = request.Id,
            Message = $"Создана новая заявка #{request.Id}",
            Type = NotificationType.StatusChange,
            SentDate = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(notification);

        await _dbContext.SaveChangesAsync();
        // Audit: request created
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = currentUserId,
            Action = "CREATE_Requests",
            EntityId = request.Id,
            EntityType = nameof(Request),
            Details = JsonSerializer.Serialize(new { managerId = currentUserId, creatorId = creatorIdToUse, priority = request.Priority.ToString() }),
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        return (true, request.Id, null, 0);
    }

    public async Task<(bool Success, string ErrorMessage, int ErrorCode)> UpdateRequestAsync(int id,
        UpdateRequestDto model, string userId)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, "Заявка не найдена.", 404);

        if (request.ManagerId != userId)
            return (false, "Только назначенный менеджер может редактировать заявку.", 403);

        request.Quantity = model.Quantity;
        request.Priority = model.Priority;
        request.Comments = model.Comments;
        request.TargetCompletion = model.TargetCompletion.HasValue
            ? DateTime.SpecifyKind(model.TargetCompletion.Value, DateTimeKind.Utc)
            : null;

        var history = new RequestHistory
        {
            RequestId = request.Id,
            NewStatusId = request.CurrentStatusId,
            ChangedByUserId = userId,
            Comment = "Заявка обновлена",
            ChangeDate = DateTime.UtcNow,
            FieldChanged = "Details"
        };
        _dbContext.RequestHistories.Add(history);

        await _dbContext.SaveChangesAsync();
        // Audit: request updated
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "UPDATE_Requests",
            EntityId = request.Id,
            EntityType = nameof(Request),
            Details = JsonSerializer.Serialize(new { quantity = request.Quantity, priority = request.Priority.ToString(), comments = request.Comments, targetCompletion = request.TargetCompletion }),
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        return (true, null, 0);
    }

    public async Task<(bool Success, string ErrorMessage, int ErrorCode)> ChangeStatusAsync(int id,
        ChangeStatusDto model, string userId, bool isTechnician)
    {
        var request = await _dbContext.Requests
            .Include(r => r.CurrentStatus)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, "Заявка не найдена.", 404);

        var newStatus = await _dbContext.RequestStatuses.FirstOrDefaultAsync(s => s.Id == model.NewStatusId);
        if (newStatus == null)
            return (false, "Статус не найден.", 404);

        if (isTechnician)
        {
            var isAssigned = await _dbContext.RequestAssignments
                .AnyAsync(ra =>
                    ra.RequestId == id && ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician);
            if (!isAssigned)
                return (false, "Техник не назначен на эту заявку.", 403);

            if (!new[] { 3, 4 }.Contains(model.NewStatusId)) // В работе, Наладка
                return (false, "Техник может устанавливать только статусы 'В работе' или 'Наладка'.", 403);
        }

        var previousStatusId = request.CurrentStatusId;
        request.CurrentStatusId = model.NewStatusId;
        var history = new RequestHistory
        {
            RequestId = request.Id,
            OldStatusId = previousStatusId,
            NewStatusId = model.NewStatusId,
            ChangedByUserId = userId,
            Comment = model.Comment,
            ChangeDate = DateTime.UtcNow
        };
        _dbContext.RequestHistories.Add(history);

        var notification = new Notification
        {
            UserId = request.CreatorId,
            RequestId = request.Id,
            Message = $"Статус заявки #{request.Id} изменён на '{newStatus.Name}'",
            Type = NotificationType.StatusChange,
            SentDate = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(notification);

        // Write audit log
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "UPDATE_Requests_Status",
            EntityId = request.Id,
            EntityType = nameof(Request),
            Details = JsonSerializer.Serialize(new { oldStatusId = previousStatusId, newStatusId = model.NewStatusId, comment = model.Comment }),
            IpAddress = null,
            Timestamp = DateTime.UtcNow
        });

        // Update status statistics (upsert for today)
        var today = DateTime.UtcNow.Date;
        var stat = await _dbContext.StatusStatistics.FirstOrDefaultAsync(s => s.StatusId == model.NewStatusId && s.Date == today);
        var totalForStatus = await _dbContext.Requests.CountAsync(r => r.CurrentStatusId == model.NewStatusId);
        if (stat == null)
        {
            stat = new StatusStatistic
            {
                StatusId = model.NewStatusId,
                Date = today,
                CountRequests = totalForStatus,
                AvgCompletionDays = null
            };
            _dbContext.StatusStatistics.Add(stat);
        }
        else
        {
            stat.CountRequests = totalForStatus;
        }

        await _dbContext.SaveChangesAsync();
        return (true, null, 0);
    }

    public async Task<(bool Success, string ErrorMessage, int ErrorCode)> AssignUserAsync(int id, AssignUserDto model,
        string managerId)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, "Заявка не найдена.", 404);

        // Allow Admins to assign regardless of manager ownership
        var caller = await _userManager.FindByIdAsync(managerId);
        var callerIsAdmin = await _userManager.IsInRoleAsync(caller, "Admin");
        if (!callerIsAdmin && request.ManagerId != managerId)
            return (false, "Только назначенный менеджер может назначать сотрудников.", 403);

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null || !user.IsActive)
            return (false, "Пользователь не найден или неактивен.", 404);

        if (!await _userManager.IsInRoleAsync(user, model.Role.ToString()))
            return (false, $"Пользователь не имеет роль {model.Role}.", 400);

        var assignment = new RequestAssignment
        {
            RequestId = id,
            UserId = model.UserId,
            RoleInRequest = model.Role,
            AssignedDate = DateTime.UtcNow
        };
        _dbContext.RequestAssignments.Add(assignment);

        var notification = new Notification
        {
            UserId = model.UserId,
            RequestId = id,
            Message = $"Вы назначены на заявку #{id} как {model.Role}.",
            Type = NotificationType.Assignment,
            SentDate = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(notification);

        await _dbContext.SaveChangesAsync();
        // Audit: assignment
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = managerId,
            Action = "CREATE_RequestAssignments",
            EntityId = assignment.Id,
            EntityType = nameof(RequestAssignment),
            Details = JsonSerializer.Serialize(new { assignedUserId = model.UserId, role = model.Role.ToString(), requestId = id }),
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        return (true, null, 0);
    }

    public async Task<(bool Success, int FileId, string ErrorMessage, int ErrorCode)> UploadFileAsync(int id,
        UploadFileDto model, string userId)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        
        if (request == null)
            return (false, 0, "Заявка не найдена.", 404);

        var currentUser = await _userManager.FindByIdAsync(userId);
        var isTechnician = await _userManager.IsInRoleAsync(currentUser, "Technician");
        var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");
        var isManager = await _userManager.IsInRoleAsync(currentUser, "Manager");
        if (isTechnician && !isAdmin && !isManager)
        {
            var isAssigned = await _dbContext.RequestAssignments
                .AnyAsync(ra =>
                    ra.RequestId == id && ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician);
            if (!isAssigned)
                return (false, 0, "Техник не назначен на эту заявку.", 403);
        }

        var uploadsRoot = Path.Combine(AppContext.BaseDirectory, "Uploads");
        Directory.CreateDirectory(uploadsRoot);
        var storedFileName = $"{Guid.NewGuid()}_{model.File.FileName}";
        var absolutePath = Path.Combine(uploadsRoot, storedFileName);
        using (var stream = new FileStream(absolutePath, FileMode.Create))
        {
            await model.File.CopyToAsync(stream);
        }

        var requestFile = new RequestFile
        {
            RequestId = id,
            // store relative path for portability, write used absolute path above
            FilePath = Path.Combine("Uploads", storedFileName),
            FileName = model.File.FileName,
            FileType = model.File.ContentType,
            Description = model.Description,
            UploadedByUserId = userId,
            IsConfidential = model.IsConfidential
        };
        _dbContext.RequestFiles.Add(requestFile);
        await _dbContext.SaveChangesAsync();
        // Audit: file upload
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "CREATE_RequestFiles",
            EntityId = requestFile.Id,
            EntityType = nameof(RequestFile),
            Details = JsonSerializer.Serialize(new { requestId = id, fileName = requestFile.FileName, contentType = requestFile.FileType }),
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        return (true, requestFile.Id, null, 0);
    }

    public async Task<(bool Success, object Request, string ErrorMessage, int ErrorCode)> GetRequestAsync(int id,
        string userId, bool isClient)
    {
        var request = await _dbContext.Requests
            .Include(r => r.Creator) // Изменено с r.Client на r.Creator
            .Include(r => r.EquipmentType)
            .Include(r => r.CurrentStatus)
            .Include(r => r.Manager)
            .Include(r => r.RequestAssignments)
            .ThenInclude(ra => ra.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, null, "Заявка не найдена.", 404);

        if (isClient)
        {
            if (request.CreatorId != userId) // Изменено с request.Client.UniqueCode на request.CreatorId
                return (false, null, "Доступ запрещён.", 403);

            return (true, (object)new
            {
                request.Id,
                EquipmentName = request.EquipmentType.Name,
                request.Quantity,
                request.Priority,
                request.CreatedDate,
                request.TargetCompletion,
                StatusName = request.CurrentStatus.Name,
                request.Comments
            }, null, 0);
        }

        var user = await _userManager.FindByIdAsync(userId);
        var isTechnician = await _userManager.IsInRoleAsync(user, "Technician");
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        var isManager = await _userManager.IsInRoleAsync(user, "Manager");
        if (isTechnician && !isAdmin && !isManager)
        {
            var isAssigned = await _dbContext.RequestAssignments
                .AnyAsync(ra =>
                    ra.RequestId == id && ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician);
            if (!isAssigned)
                return (false, null, "Техник не назначен на эту заявку.", 403);
        }

        return (true, (object)new
        {
            request.Id,
            Creator = new // Изменено с Client на Creator
            {
                request.Creator.Id, request.Creator.FullName, request.Creator.Email
            }, // Изменено на данные ApplicationUser
            Equipment = new
                { request.EquipmentType.Id, EquipmentName = request.EquipmentType.Name, request.EquipmentType.Price },
            request.Quantity,
            request.Priority,
            request.CreatedDate,
            request.TargetCompletion,
            Status = new { request.CurrentStatus.Id, StatusName = request.CurrentStatus.Name },
            Manager = request.Manager != null ? new { request.Manager.Id, request.Manager.FullName } : null,
            Assignments = request.RequestAssignments.Select(ra => new
                { ra.UserId, ra.User.FullName, ra.RoleInRequest, ra.AssignedDate }),
            request.Comments
        }, null, 0);
    }

    public async Task<List<object>> GetRequestsAsync(RequestFilterDto filter, string userId, bool isClient,
        bool isTechnician)
    {
        var query = _dbContext.Requests
            .Include(r => r.Creator) // Изменено с r.Client на r.Creator
            .Include(r => r.EquipmentType)
            .Include(r => r.CurrentStatus)
            .AsQueryable();

        if (isClient)
            query = query.Where(r => r.CreatorId == userId); // Изменено с r.Client.UniqueCode на r.CreatorId
        else if (isTechnician)
            query = query.Where(r =>
                r.RequestAssignments.Any(ra =>
                    ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician));

        if (filter.StatusId.HasValue)
            query = query.Where(r => r.CurrentStatusId == filter.StatusId);
        if (filter.ClientId.HasValue)
            // query = query.Where(r => r.ClientId == filter.ClientId); // Закомментировано
            if (filter.Priority.HasValue)
                query = query.Where(r => r.Priority == filter.Priority);
        if (filter.StartDate.HasValue)
            query = query.Where(r => r.CreatedDate >= filter.StartDate);
        if (filter.EndDate.HasValue)
            query = query.Where(r => r.CreatedDate <= filter.EndDate);

        query = query.Where(r => r.IsActive);

        return await query
            .Select(r => isClient
                ? (object)new
                {
                    r.Id,
                    EquipmentName = r.EquipmentType.Name,
                    r.Quantity,
                    r.Priority,
                    r.CreatedDate,
                    r.TargetCompletion,
                    StatusName = r.CurrentStatus.Name,
                    r.Comments
                }
                : (object)new
                {
                    r.Id,
                    Creator = new { r.Creator.Id, r.Creator.FullName, r.Creator.Email }, // Изменено с Client на Creator
                    Equipment = new { r.EquipmentType.Id, EquipmentName = r.EquipmentType.Name, r.EquipmentType.Price },
                    r.Quantity,
                    r.Priority,
                    r.CreatedDate,
                    r.TargetCompletion,
                    Status = new { r.CurrentStatus.Id, StatusName = r.CurrentStatus.Name },
                    r.Comments
                })
            .ToListAsync();
    }

    public async Task<(bool Success, List<object> History, string ErrorMessage, int ErrorCode)> GetRequestHistoryAsync(
        int id, string userId, bool isClient)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, null, "Заявка не найдена.", 404);

        if (isClient && request.CreatorId != userId) // Изменено с request.Client.UniqueCode на request.CreatorId
            return (false, null, "Доступ запрещён.", 403);

        var user = await _userManager.FindByIdAsync(userId);
        var isTechnician = await _userManager.IsInRoleAsync(user, "Technician");
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        var isManager = await _userManager.IsInRoleAsync(user, "Manager");
        if (isTechnician && !isAdmin && !isManager)
        {
            var isAssigned = await _dbContext.RequestAssignments
                .AnyAsync(ra =>
                    ra.RequestId == id && ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician);
            if (!isAssigned)
                return (false, null, "Техник не назначен на эту заявку.", 403);
        }

        var history = await _dbContext.RequestHistories
            .Include(h => h.NewStatus)
            .Include(h => h.OldStatus)
            .Include(h => h.ChangedByUser)
            .Where(h => h.RequestId == id)
            .Select(h => isClient
                ? (object)new
                {
                    h.Id,
                    StatusName = h.NewStatus.Name,
                    h.ChangeDate,
                    h.Comment
                }
                : (object)new
                {
                    h.Id,
                    OldStatus = h.OldStatus != null ? new { h.OldStatus.Id, StatusName = h.OldStatus.Name } : null,
                    NewStatus = new { h.NewStatus.Id, StatusName = h.NewStatus.Name },
                    ChangedBy = new { h.ChangedByUser.Id, h.ChangedByUser.FullName },
                    h.ChangeDate,
                    h.Comment,
                    h.FieldChanged
                })
            .ToListAsync();

        return (true, history, null, 0);
    }

    public async Task<(bool Success, string ErrorMessage, int ErrorCode)> DeleteRequestAsync(int id)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null)
            return (false, "Заявка не найдена.", 404);

        request.IsActive = false; // Логическое удаление
        await _dbContext.SaveChangesAsync();
        // Audit: comment added
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = request.ManagerId,
            Action = "CREATE_RequestComments",
            EntityId = id,
            EntityType = nameof(Request),
            Details = request.Comments,
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        return (true, null, 0);
    }

    public async Task<(bool Success, string ErrorMessage, int ErrorCode)> AddCommentToRequestAsync(
        int id, AddCommentDto model, string userId)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, "Заявка не найдена.", 404);

        // Check if the user is associated with the request (Creator, Manager, Technician)
        var isAssociated = request.CreatorId == userId ||
                           request.ManagerId == userId ||
                           await _dbContext.RequestAssignments.AnyAsync(ra =>
                               ra.RequestId == id && ra.UserId == userId);

        if (!isAssociated)
            return (false, "У вас нет прав для добавления комментария к этой заявке.", 403);

        var history = new RequestHistory
        {
            RequestId = id,
            OldStatusId = request.CurrentStatusId,
            NewStatusId = request.CurrentStatusId, // Status does not change with a comment
            ChangedByUserId = userId,
            Comment = model.Comment,
            ChangeDate = DateTime.UtcNow,
            FieldChanged = "Comment"
        };
        _dbContext.RequestHistories.Add(history);

        var notification = new Notification
        {
            UserId = request.CreatorId, // Notify the creator
            RequestId = id,
            Message = $"К заявке #{id} добавлен новый комментарий.",
            Type = NotificationType.StatusChange, // Can be a new type like CommentAdded
            SentDate = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(notification);

        // Notify manager if different from creator
        if (request.ManagerId != null && request.ManagerId != request.CreatorId)
        {
            _dbContext.Notifications.Add(new Notification
            {
                UserId = request.ManagerId,
                RequestId = id,
                Message = $"К заявке #{id} добавлен новый комментарий.",
                Type = NotificationType.StatusChange,
                SentDate = DateTime.UtcNow
            });
        }

        // Notify assigned technicians
        var assignedTechnicians = await _dbContext.RequestAssignments
            .Where(ra => ra.RequestId == id && ra.RoleInRequest == RequestAssignmentRole.Technician)
            .Select(ra => ra.UserId)
            .ToListAsync();
        foreach (var technicianId in assignedTechnicians.Where(tId =>
                     tId != userId && tId != request.CreatorId && tId != request.ManagerId))
        {
            _dbContext.Notifications.Add(new Notification
            {
                UserId = technicianId,
                RequestId = id,
                Message = $"К заявке #{id} добавлен новый комментарий.",
                Type = NotificationType.StatusChange,
                SentDate = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return (true, null, 0);
    }
}