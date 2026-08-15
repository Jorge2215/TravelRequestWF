using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TravelRequestWF.Infrastructure.Services;

public class PowerAutomateNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PowerAutomateNotificationService> _logger;
    private readonly string _flowAUrl;
    private readonly string _flowBUrl;

    public PowerAutomateNotificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PowerAutomateNotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _flowAUrl = configuration["PowerAutomate:FlowASubmissionUrl"] ?? string.Empty;
        _flowBUrl = configuration["PowerAutomate:FlowBStatusChangeUrl"] ?? string.Empty;
    }

    public async Task NotifyRequestSubmittedAsync(NotificationPayload payload)
        => await PostToFlowAsync(_flowAUrl, "Flow A (Submission)", payload);

    public async Task NotifyRequestStatusChangedAsync(NotificationPayload payload)
        => await PostToFlowAsync(_flowBUrl, "Flow B (Status Change)", payload);

    private async Task PostToFlowAsync(string url, string flowName, NotificationPayload payload)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Power Automate {FlowName} URL not configured — skipping notification for RequestId={RequestId}.", flowName, payload.RequestId);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Power Automate {FlowName} returned non-success status {StatusCode} for RequestId={RequestId}.", flowName, (int)response.StatusCode, payload.RequestId);
            else
                _logger.LogInformation("Power Automate {FlowName} notified successfully for RequestId={RequestId}.", flowName, payload.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Power Automate {FlowName} notification failed for RequestId={RequestId}. Notification is non-blocking — workflow continues.", flowName, payload.RequestId);
        }
    }
}
