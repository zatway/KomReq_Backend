using Infrastructure.DbContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Models.Dtos;
using Platform.Models.Enums;
using Platform.Models.Request.Request;
using Platform.Models.Response.Request;
using Platform.Models.Users;

namespace Application.Service;

public class RequestService
{
    private readonly KomReqDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public RequestService(KomReqDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<(bool Success, int RequestId, string ErrorMessage, int ErrorCode)> CreateRequestAsync(
        CreateRequestDto model, string managerId)
    {
        if (!await _dbContext.Clients.AnyAsync(c => c.Id == model.ClientId))
            return (false, 0, "Клиент не найден.", 404);

        if (!await _dbContext.EquipmentTypes.AnyAsync(e => e.Id == model.EquipmentTypeId && e.IsActive))
            return (false, 0, "Тип оборудования не найден или неактивен.", 404);

        var request = new Request
        {
            ClientId = model.ClientId,
            EquipmentTypeId = model.EquipmentTypeId,
            Quantity = model.Quantity,
            Priority = model.Priority,
            CreatedDate = DateTime.UtcNow,
            ManagerId = managerId,
            CurrentStatusId = 1, // Новая
            Comments = model.Comments,
            TargetCompletion = model.TargetCompletion
        };

        _dbContext.Requests.Add(request);
        await _dbContext.SaveChangesAsync();

        var history = new RequestHistory
        {
            RequestId = request.Id,
            NewStatusId = 1,
            ChangedByUserId = managerId,
            Comment = "Заявка создана",
            ChangeDate = DateTime.UtcNow
        };
        _dbContext.RequestHistories.Add(history);

        var notification = new Notification
        {
            ClientId = model.ClientId,
            RequestId = request.Id,
            Message = $"Создана новая заявка #{request.Id}",
            Type = NotificationType.StatusChange,
            SentDate = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(notification);

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
        request.TargetCompletion = model.TargetCompletion;

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

        request.CurrentStatusId = model.NewStatusId;
        var history = new RequestHistory
        {
            RequestId = request.Id,
            OldStatusId = request.CurrentStatusId,
            NewStatusId = model.NewStatusId,
            ChangedByUserId = userId,
            Comment = model.Comment,
            ChangeDate = DateTime.UtcNow
        };
        _dbContext.RequestHistories.Add(history);

        var notification = new Notification
        {
            ClientId = request.ClientId,
            RequestId = request.Id,
            Message = $"Статус заявки #{request.Id} изменён на '{newStatus.Name}'",
            Type = NotificationType.StatusChange,
            SentDate = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(notification);

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

        if (request.ManagerId != managerId)
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
        return (true, null, 0);
    }

    public async Task<(bool Success, int FileId, string ErrorMessage, int ErrorCode)> UploadFileAsync(int id,
        UploadFileDto model, string userId)
    {
        var request = await _dbContext.Requests
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
        if (request == null)
            return (false, 0, "Заявка не найдена.", 404);

        var isTechnician = await _userManager.IsInRoleAsync(await _userManager.FindByIdAsync(userId), "Technician");
        if (isTechnician)
        {
            var isAssigned = await _dbContext.RequestAssignments
                .AnyAsync(ra =>
                    ra.RequestId == id && ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician);
            if (!isAssigned)
                return (false, 0, "Техник не назначен на эту заявку.", 403);
        }

        var filePath = Path.Combine("Uploads", $"{Guid.NewGuid()}_{model.File.FileName}");
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await model.File.CopyToAsync(stream);
        }

        var requestFile = new RequestFile
        {
            RequestId = id,
            FilePath = filePath,
            FileName = model.File.FileName,
            FileType = model.File.ContentType,
            Description = model.Description,
            UploadedByUserId = userId,
            IsConfidential = model.IsConfidential
        };
        _dbContext.RequestFiles.Add(requestFile);
        await _dbContext.SaveChangesAsync();

        return (true, requestFile.Id, null, 0);
    }

    public async Task<(bool Success, object Request, string ErrorMessage, int ErrorCode)> GetRequestAsync(int id,
        string userId, bool isClient)
    {
        var request = await _dbContext.Requests
            .Include(r => r.Client)
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
            if (request.Client.UniqueCode.ToString() != userId)
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

        var isTechnician = await _userManager.IsInRoleAsync(await _userManager.FindByIdAsync(userId), "Technician");
        if (isTechnician)
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
            Client = new
                { request.Client.Id, request.Client.FullName, request.Client.CompanyName, request.Client.Email },
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
            .Include(r => r.Client)
            .Include(r => r.EquipmentType)
            .Include(r => r.CurrentStatus)
            .AsQueryable();

        if (isClient)
            query = query.Where(r => r.Client.UniqueCode.ToString() == userId);
        else if (isTechnician)
            query = query.Where(r =>
                r.RequestAssignments.Any(ra =>
                    ra.UserId == userId && ra.RoleInRequest == RequestAssignmentRole.Technician));

        if (filter.StatusId.HasValue)
            query = query.Where(r => r.CurrentStatusId == filter.StatusId);
        if (filter.ClientId.HasValue)
            query = query.Where(r => r.ClientId == filter.ClientId);
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
                    Client = new { r.Client.Id, r.Client.FullName, r.Client.CompanyName, r.Client.Email },
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

        if (isClient && request.Client.UniqueCode.ToString() != userId)
            return (false, null, "Доступ запрещён.", 403);

        var isTechnician = await _userManager.IsInRoleAsync(await _userManager.FindByIdAsync(userId), "Technician");
        if (isTechnician)
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
        return (true, null, 0);
    }
}