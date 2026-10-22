public class ReportService
{
    private readonly AdoNetDbService _db;

    public ReportService(AdoNetDbService db)
    {
        _db = db;
    }

    public (bool Ok, string? Error, int Id) SubmitReport(int reporterId, string reporterName, int? productId, string? productTitle, int? userId, string? userName, string reason, string? description)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Please select a reason for the report.", 0);
        if (description?.Length > 1000)
            return (false, "Description must be under 1,000 characters.", 0);

        var report = new Report
        {
            ReporterId = reporterId,
            ReporterName = reporterName.Trim(),
            ReportedProductId = productId,
            ReportedProductTitle = productTitle,
            ReportedUserId = userId,
            ReportedUserName = userName,
            Reason = reason.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var id = _db.AddReport(report);
        return (true, null, id);
    }

    public List<Report> GetAllReports() => _db.GetAllReports();

    public int GetPendingCount() => _db.GetPendingReportsCount();

    public bool ResolveReport(int reportId, ReportStatus status, string? notes) =>
        _db.UpdateReportStatus(reportId, status, notes);
}
