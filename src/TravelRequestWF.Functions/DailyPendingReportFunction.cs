using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Entities;

namespace TravelRequestWF.Functions;

public class DailyPendingReportFunction
{
    private readonly AppDbContext _db;
    private readonly ILogger<DailyPendingReportFunction> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _flowCUrl;

    public DailyPendingReportFunction(
        AppDbContext db,
        ILogger<DailyPendingReportFunction> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _flowCUrl = configuration["PowerAutomate:FlowCDailyDigestUrl"] ?? string.Empty;
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
        _logger.LogInformation("[DailyReport] Starting daily pending travel requests digest. UTC: {UtcNow}", utcNow);

        var pending = await _db.TravelRequests
            .Where(r => r.Status == TravelRequestStatus.Pending)
            .Include(r => r.Employee)
            .Include(r => r.Approver)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogInformation("[DailyReport] No pending travel requests found as of {UtcNow}.", utcNow);
            return;
        }

        _logger.LogInformation("[DailyReport] {Count} pending travel request(s) found. Grouping by manager...", pending.Count);

        var byManager = pending.GroupBy(r => r.ApproverId);
        int managerCount = 0;
        int failureCount = 0;

        foreach (var group in byManager)
        {
            var manager = group.First().Approver;
            var payload = new ManagerDigestPayload(
                ManagerName: manager.Name,
                ManagerEmail: manager.Email,
                PendingRequests: group.Select(r => new PendingRequestItem(
                    RequestId: r.Id,
                    EmployeeName: r.Employee.Name,
                    Destination: r.Destination,
                    StartDate: r.StartDate.ToString("yyyy-MM-dd"),
                    EndDate: r.EndDate.ToString("yyyy-MM-dd"),
                    Status: r.Status.ToString()
                )).ToList()
            );

            bool success = await PostDigestAsync(payload);
            managerCount++;
            if (!success) failureCount++;
        }

        _logger.LogInformation(
            "[DailyReport] Digest complete. {TotalRequests} pending request(s), {ManagerCount} manager(s) notified, {FailureCount} failure(s). UTC: {UtcNow:yyyy-MM-dd HH:mm}",
            pending.Count,
            managerCount,
            failureCount,
            utcNow);
    }

    private async Task<bool> PostDigestAsync(ManagerDigestPayload payload)
    {
        if (string.IsNullOrWhiteSpace(_flowCUrl) || _flowCUrl.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[DailyReport] Flow C URL not configured — skipping digest for manager {ManagerEmail}.", payload.ManagerEmail);
            return true;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(_flowCUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[DailyReport] Flow C returned non-success status {StatusCode} for manager {ManagerEmail}.", (int)response.StatusCode, payload.ManagerEmail);
                return false;
            }

            _logger.LogInformation("[DailyReport] Flow C digest sent successfully for manager {ManagerEmail}.", payload.ManagerEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DailyReport] Flow C digest failed for manager {ManagerEmail}. Non-blocking — continuing.", payload.ManagerEmail);
            return false;
        }
    }
}
