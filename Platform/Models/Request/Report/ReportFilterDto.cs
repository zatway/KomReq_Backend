using System.ComponentModel.DataAnnotations;
using Platform.Models.Enums;

namespace Platform.Models.Request.Report;

public class ReportFilterDto
{
    public int? StatusId { get; set; }
    public RequestPriority? Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ClientUserId { get; set; }
}

