using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.DbContext;
using Platform.Models.Dtos;
using Microsoft.AspNetCore.Authorization;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RequestStatusController : ControllerBase
{
    private readonly KomReqDbContext _dbContext;

    public RequestStatusController(KomReqDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Technician,Client")] // Accessible by all roles
    public async Task<IActionResult> GetRequestStatuses()
    {
        var statuses = await _dbContext.RequestStatuses
            .OrderBy(s => s.OrderNum)
            .Select(s => new { s.Id, s.Name, s.IsFinal })
            .ToListAsync();
        return Ok(statuses);
    }
}

