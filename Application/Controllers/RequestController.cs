using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Application.Service;
using Infrastructure.DbContext;
using Platform.Models.Request.Request;
using Platform.Models.Response.Request;
using Platform.Models.Users;
using Microsoft.Extensions.Logging;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RequestController : ControllerBase
{
    private readonly KomReqDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RequestService _requestService;
    private readonly ILogger<RequestController> _logger;

    public RequestController(
        KomReqDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RequestService requestService,
        ILogger<RequestController> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _requestService = requestService;
        _logger = logger;
    }

    // Создание новой заявки (менеджеры)
    [Authorize(Roles = "Manager")]
    [HttpPost("create")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateRequestDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation("Current User ID: {UserId}", currentUserId);
        var result = await _requestService.CreateRequestAsync(model, currentUserId); // Возвращено на FindFirstValue
        if (!result.Success)
            return BadRequest(new { Message = result.ErrorMessage });

        return Ok(new { Message = "Заявка создана.", RequestId = result.RequestId });
    }

    // Редактирование заявки (менеджеры)
    [Authorize(Roles = "Manager")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRequest(int id, [FromBody] UpdateRequestDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _requestService.UpdateRequestAsync(id, model, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : BadRequest(new { Message = result.ErrorMessage });

        return Ok(new { Message = "Заявка обновлена." });
    }

    // Изменение статуса заявки (менеджеры и техники)
    [Authorize(Roles = "Manager,Technician")]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeStatusDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var isTechnician = User.IsInRole("Technician");
        var result = await _requestService.ChangeStatusAsync(id, model, User.FindFirstValue(ClaimTypes.NameIdentifier), isTechnician);
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : BadRequest(new { Message = result.ErrorMessage });

        return Ok(new { Message = "Статус заявки изменён." });
    }

    // Назначение сотрудника (менеджеры)
    [Authorize(Roles = "Manager")]
    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignUser(int id, [FromBody] AssignUserDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _requestService.AssignUserAsync(id, model, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : BadRequest(new { Message = result.ErrorMessage });

        return Ok(new { Message = "Сотрудник назначен." });
    }

    // Прикрепление файла (менеджеры и техники)
    [Authorize(Roles = "Manager,Technician")]
    [HttpPost("{id}/files")]
    public async Task<IActionResult> UploadFile(int id, [FromForm] UploadFileDto model)
    {
        if (!ModelState.IsValid || model.File == null)
            return BadRequest(new { Message = "Файл обязателен." });

        var result = await _requestService.UploadFileAsync(id, model, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : BadRequest(new { Message = result.ErrorMessage });

        return Ok(new { Message = "Файл прикреплён.", FileId = result.FileId });
    }

    // Просмотр заявки (все роли)
    [Authorize(Roles = "Manager,Technician,Client,Admin")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetRequest(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isClient = User.IsInRole("Client");
        var result = await _requestService.GetRequestAsync(id, userId, isClient);
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : Forbid(result.ErrorMessage);

        return Ok(result.Request);
    }

    // Просмотр списка заявок с фильтрацией (все роли)
    [Authorize(Roles = "Manager,Technician,Client,Admin")]
    [HttpGet]
    public async Task<IActionResult> GetRequests([FromQuery] RequestFilterDto filter)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isClient = User.IsInRole("Client");
        var isTechnician = User.IsInRole("Technician");
        var requests = await _requestService.GetRequestsAsync(filter, userId, isClient, isTechnician);
        return Ok(requests);
    }

    // Просмотр истории заявки (все роли)
    [Authorize(Roles = "Manager,Technician,Client,Admin")]
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetRequestHistory(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isClient = User.IsInRole("Client");
        var result = await _requestService.GetRequestHistoryAsync(id, userId, isClient);
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : Forbid(result.ErrorMessage);

        return Ok(result.History);
    }

    // Удаление заявки (администраторы)
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRequest(int id)
    {
        var result = await _requestService.DeleteRequestAsync(id);
        if (!result.Success)
            return NotFound(new { Message = result.ErrorMessage });

        return Ok(new { Message = "Заявка удалена." });
    }

    [HttpPost("{id}/add-comment")]
    [Authorize(Roles = "Manager,Technician,Client")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _requestService.AddCommentToRequestAsync(id, model, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!result.Success)
            return result.ErrorCode == 404 ? NotFound(new { Message = result.ErrorMessage }) : Forbid(result.ErrorMessage);

        return Ok(new { Message = "Комментарий добавлен." });
    }
}