using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.DbContext;
using Microsoft.AspNetCore.Authorization;
using Platform.Models.Dtos;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatisticController : ControllerBase
{
    private readonly KomReqDbContext _dbContext;

    public StatisticController(KomReqDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("status")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetStatusStatistics(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var query = _dbContext.StatusStatistics.Include(ss => ss.Status).AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(ss => ss.Date >= startDate.Value.Date);
        }

        if (endDate.HasValue)
        {
            query = query.Where(ss => ss.Date <= endDate.Value.Date);
        }

        var statistics = await query
            .OrderBy(ss => ss.Date)
            .Select(ss => new
            {
                ss.Id,
                StatusId = ss.Status.Id,
                StatusName = ss.Status.Name,
                ss.Date,
                ss.CountRequests,
                ss.AvgCompletionDays
            })
            .ToListAsync();

        return Ok(statistics);
    }
}

