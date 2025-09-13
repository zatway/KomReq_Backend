using Platform.Models.Enums;

namespace Platform.Models.Response.Request;

public class RequestFilterDto
{
    public int? StatusId { get; set; }
    public int? ClientId { get; set; }
    public RequestPriority? Priority { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}