using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Functions;

public class DailyPendingReportFunction
{
    private readonly AppDbContext _db;
    private readonly ILogger<DailyPendingReportFunction> _logger;

    public DailyPendingReportFunction(AppDbContext db, ILogger<DailyPendingReportFunction> logger)
    {
        _db = db;
        _logger = logger;
    }

    // NCRONTAB six-part format: {seconds} {minutes} {hours} {day} {month} {weekday}
    // "0 0 8 * * *" = 08:00:00 UTC daily.
    // If 8 AM Argentina time (ART, UTC-3) is needed, use "0 0 11 * * *" instead.
    [Function(nameof(DailyPendingReportFunction))]
    public async Task RunAsync(
        [TimerTrigger("0 0 8 * * *")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        _logger.LogInformation("[DailyReport] Starting daily pending travel requests report. UTC: {UtcNow}", utcNow);

        var pendingRequests = await _db.TravelRequests
            .Where(r => r.Status == TravelRequestStatus.Pending)
            .Include(r => r.Employee)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);

        if (pendingRequests.Count == 0)
        {
            _logger.LogInformation("[DailyReport] No pending travel requests found as of {UtcNow}.", utcNow);
            return;
        }

        _logger.LogInformation("[DailyReport] {Count} pending travel request(s) found:", pendingRequests.Count);

        foreach (var r in pendingRequests)
        {
            _logger.LogInformation(
                "[DailyReport] Id={Id} | Employee={EmployeeName} ({Email}) | Destination={Destination} | StartDate={StartDate:yyyy-MM-dd} | EndDate={EndDate:yyyy-MM-dd} | Status={Status} | SubmittedAt={SubmittedAt:yyyy-MM-dd HH:mm}",
                r.Id,
                r.Employee.Name,
                r.Employee.Email,
                r.Destination,
                r.StartDate,
                r.EndDate,
                r.Status,
                r.SubmittedAt);
        }

        _logger.LogInformation(
            "[DailyReport] Report complete. {Count} pending travel request(s) require attention as of {UtcNow:yyyy-MM-dd HH:mm} UTC.",
            pendingRequests.Count,
            utcNow);
    }
}
