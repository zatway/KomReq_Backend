using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.DbContext;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Platform.Models.Dtos;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationController : ControllerBase
{
    private readonly KomReqDbContext _dbContext;

    public NotificationController(KomReqDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyNotifications([FromQuery] bool? unreadOnly = false)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var query = _dbContext.Notifications
            .Include(n => n.Request)
            .Where(n => n.UserId == userId)
            .AsQueryable();

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.SentDate)
            .Select(n => new
            {
                n.Id,
                n.RequestId,
                RequestSubject = n.Request != null ? $"Заявка #{n.Request.Id}" : null,
                n.Type,
                n.Message,
                n.SentDate,
                n.IsRead
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPost("{id}/mark-read")]
    [Authorize]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var notification = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification == null)
        {
            return NotFound("Уведомление не найдено или у вас нет прав доступа.");
        }

        notification.IsRead = true;
        _dbContext.Notifications.Update(notification);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("mark-all-read")]
    [Authorize]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var unreadNotifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unreadNotifications.Any())
        {
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }
            _dbContext.Notifications.UpdateRange(unreadNotifications);
            await _dbContext.SaveChangesAsync();
        }

        return NoContent();
    }
}

