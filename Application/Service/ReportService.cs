using Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using System.IO;
using System.Security.Claims;
using Platform.Models.Dtos;
using Platform.Models.Request.Report;

namespace Application.Service;

public class ReportService
{
    private readonly KomReqDbContext _dbContext;

    public ReportService(KomReqDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]> GenerateRequestReportPdf(ReportFilterDto filter)
    {
        var requests = await GetFilteredRequests(filter);


        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.PageColor(QuestPDF.Helpers.Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Times New Roman"));

                page.Header()
                    .AlignCenter() // Apply AlignCenter to the header container
                    .Text("Отчет по заявкам")
                    .SemiBold().FontSize(24);

                page.Content()
                    .Column(column =>
                    {
                        column.Spacing(5);

                        foreach (var req in requests)
                        {
                            column.Item().Text($"Заявка #{req.Id}").SemiBold();
                            column.Item().Text($"Клиент: {req.Creator.FullName ?? req.Creator.UserName}");
                            column.Item().Text($"Оборудование: {req.EquipmentType.Name}");
                            column.Item().Text($"Количество: {req.Quantity}");
                            column.Item().Text($"Приоритет: {req.Priority}");
                            column.Item().Text($"Статус: {req.CurrentStatus.Name}");
                        }

                    });

                page.Footer()
                    .AlignCenter() // Apply AlignCenter to the footer container
                    .Text(x =>
                    {
                        x.Span("Страница ").FontSize(10);
                        x.CurrentPageNumber().FontSize(10);
                        x.Span(" из ").FontSize(10);
                        x.TotalPages().FontSize(10);
                    });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateRequestReportExcel(ReportFilterDto filter)
    {
        var requests = await GetFilteredRequests(filter);

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Заявки");
            worksheet.Cell(1, 1).Value = "ID Заявки";
            worksheet.Cell(1, 2).Value = "Клиент";
            worksheet.Cell(1, 3).Value = "Оборудование";
            worksheet.Cell(1, 4).Value = "Количество";
            worksheet.Cell(1, 5).Value = "Приоритет";
            worksheet.Cell(1, 6).Value = "Статус";
            worksheet.Cell(1, 7).Value = "Дата создания";
            worksheet.Cell(1, 8).Value = "Менеджер";

            for (int i = 0; i < requests.Count; i++)
            {
                var req = requests[i];
                int row = i + 2;
                worksheet.Cell(row, 1).Value = req.Id;
                worksheet.Cell(row, 2).Value = req.Creator.FullName ?? req.Creator.UserName;
                worksheet.Cell(row, 3).Value = req.EquipmentType.Name;
                worksheet.Cell(row, 4).Value = req.Quantity;
                worksheet.Cell(row, 5).Value = req.Priority.ToString();
                worksheet.Cell(row, 6).Value = req.CurrentStatus.Name;
                worksheet.Cell(row, 7).Value = req.CreatedDate.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(row, 8).Value = req.Manager?.FullName ?? req.Manager?.UserName ?? "N/A";
            }

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }

    private async Task<List<Request>> GetFilteredRequests(ReportFilterDto filter)
    {
        // Normalize incoming dates to UTC to satisfy Npgsql timestamptz requirements
        DateTime? startUtc = filter.StartDate.HasValue
            ? (filter.StartDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(filter.StartDate.Value, DateTimeKind.Utc)
                : filter.StartDate.Value.ToUniversalTime())
            : null;
        DateTime? endUtc = filter.EndDate.HasValue
            ? (filter.EndDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(filter.EndDate.Value, DateTimeKind.Utc)
                : filter.EndDate.Value.ToUniversalTime())
            : null;

        var query = _dbContext.Requests
            .Include(r => r.Creator)
            .Include(r => r.EquipmentType)
            .Include(r => r.CurrentStatus)
            .Include(r => r.Manager)
            .Where(r => r.IsActive)
            .AsQueryable();

        if (filter.StatusId.HasValue)
        {
            query = query.Where(r => r.CurrentStatusId == filter.StatusId.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(r => r.Priority == filter.Priority.Value);
        }

        if (startUtc.HasValue)
        {
            query = query.Where(r => r.CreatedDate >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            query = query.Where(r => r.CreatedDate <= endUtc.Value);
        }

        if (!string.IsNullOrEmpty(filter.ClientUserId))
        {
            query = query.Where(r => r.CreatorId == filter.ClientUserId);
        }

        return await query.ToListAsync();
    }
}
