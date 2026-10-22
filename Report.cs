public enum ReportStatus
{
    Pending,
    Resolved,
    Dismissed
}

public class Report
{
    public int Id { get; set; }
    public int ReporterId { get; set; }
    public string ReporterName { get; set; } = "";
    public int? ReportedProductId { get; set; }
    public string? ReportedProductTitle { get; set; }
    public int? ReportedUserId { get; set; }
    public string? ReportedUserName { get; set; }
    public string Reason { get; set; } = "";
    public string? Description { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
