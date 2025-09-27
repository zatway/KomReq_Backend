using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Infrastructure.DbContext;
using Platform.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Platform.Models.Request.EquipmentType; // Add this import

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EquipmentTypeController : ControllerBase
{
    private readonly KomReqDbContext _dbContext;

    public EquipmentTypeController(KomReqDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Roles = "Manager,Admin,Technician,Client")] // Accessible by all roles for dropdowns
    public async Task<IActionResult> GetActiveEquipmentTypes()
    {
        var equipmentTypes = await _dbContext.EquipmentTypes
            .Where(et => et.IsActive)
            .Select(et => new { et.Id, et.Name })
            .ToListAsync();
        return Ok(equipmentTypes);
    }

    [HttpGet("all")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAllEquipmentTypes()
    {
        var equipmentTypes = await _dbContext.EquipmentTypes.ToListAsync();
        return Ok(equipmentTypes);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetEquipmentTypeById(int id)
    {
        var equipmentType = await _dbContext.EquipmentTypes.FindAsync(id);
        if (equipmentType == null)
        {
            return NotFound("Тип оборудования не найден.");
        }

        return Ok(equipmentType);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateEquipmentType([FromBody] CreateEquipmentTypeDto dto)
    {
        if (await _dbContext.EquipmentTypes.AnyAsync(et => et.Name == dto.Name))
        {
            return BadRequest("Тип оборудования с таким именем уже существует.");
        }

        string? normalizedSpecs = NormalizeJson(dto.Specifications);

        var equipmentType = new EquipmentType
        {
            Name = dto.Name,
            Description = dto.Description,
            Specifications = normalizedSpecs,
            Price = dto.Price,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.EquipmentTypes.Add(equipmentType);
        await _dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetEquipmentTypeById), new { id = equipmentType.Id }, equipmentType);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEquipmentType(int id, [FromBody] UpdateEquipmentTypeDto dto)
    {
        var equipmentType = await _dbContext.EquipmentTypes.FindAsync(id);
        if (equipmentType == null)
        {
            return NotFound("Тип оборудования не найден.");
        }

        if (!string.IsNullOrEmpty(dto.Name) &&
            await _dbContext.EquipmentTypes.AnyAsync(et => et.Name == dto.Name && et.Id != id))
        {
            return BadRequest("Тип оборудования с таким именем уже существует.");
        }

        equipmentType.Name = dto.Name ?? equipmentType.Name;
        equipmentType.Description = dto.Description ?? equipmentType.Description;
        equipmentType.Specifications = dto.Specifications != null
            ? NormalizeJson(dto.Specifications)
            : equipmentType.Specifications;
        equipmentType.Price = dto.Price ?? equipmentType.Price;
        equipmentType.IsActive = dto.IsActive ?? equipmentType.IsActive;

        _dbContext.EquipmentTypes.Update(equipmentType);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEquipmentType(int id)
    {
        var equipmentType = await _dbContext.EquipmentTypes.FindAsync(id);
        if (equipmentType == null)
        {
            return NotFound("Тип оборудования не найден.");
        }

        // Soft delete
        equipmentType.IsActive = false;
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    static string? NormalizeJson(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(trimmed);
            return trimmed;
        }
        catch
        {
            return System.Text.Json.JsonSerializer.Serialize(trimmed);
        }
    }
}