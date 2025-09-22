using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.DbContext;
using Microsoft.AspNetCore.Authorization;
using Platform.Models.Dtos;
using Platform.Models.Users;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditLogController : ControllerBase
{
    private readonly KomReqDbContext _dbContext;

    public AuditLogController(KomReqDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var query = _dbContext.AuditLogs.Include(al => al.User).AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(al => al.UserId == userId);
        }

        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(al => al.Action.Contains(action));
        }

        if (startDate.HasValue)
        {
            query = query.Where(al => al.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(al => al.Timestamp <= endDate.Value);
        }

        var auditLogs = await query
            .OrderByDescending(al => al.Timestamp)
            .Select(al => new
            {
                al.Id,
                UserId = al.User.Id,
                UserName = al.User.UserName,
                UserFullName = al.User.FullName,
                al.Action,
                al.EntityId,
                al.EntityType,
                al.Details,
                al.IpAddress,
                al.Timestamp
            })
            .ToListAsync();

        return Ok(auditLogs);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAuditLogById(int id)
    {
        var auditLog = await _dbContext.AuditLogs.Include(al => al.User)
            .FirstOrDefaultAsync(al => al.Id == id);

        if (auditLog == null)
        {
            return NotFound("Лог аудита не найден.");
        }

        return Ok(new
        {
            auditLog.Id,
            UserId = auditLog.User.Id,
            UserName = auditLog.User.UserName,
            UserFullName = auditLog.User.FullName,
            auditLog.Action,
            auditLog.EntityId,
            auditLog.EntityType,
            auditLog.Details,
            auditLog.IpAddress,
            auditLog.Timestamp
        });
    }
}

