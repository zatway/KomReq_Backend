using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Application.Service;
using Platform.Models.Request.Report;

namespace Application.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly ReportService _reportService;

    public ReportController(ReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("requests-pdf")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetRequestsReportPdf([FromQuery] ReportFilterDto filter)
    {
        try
        {
            var pdfBytes = await _reportService.GenerateRequestReportPdf(filter);
            return File(pdfBytes, "application/pdf", "requests-report.pdf");
        }
        catch (Exception ex)
        {
            return Problem(title: "Ошибка генерации PDF", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpGet("requests-excel")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetRequestsReportExcel([FromQuery] ReportFilterDto filter)
    {
        var excelBytes = await _reportService.GenerateRequestReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "requests-report.xlsx");
    }
}

