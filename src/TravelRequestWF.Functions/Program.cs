using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using TravelRequestWF.Infrastructure.Data;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

builder.Services.AddHttpClient();

// Defer SqlConnectionString resolution to first use so a missing/placeholder value
// does NOT crash the host at startup (which would cause "sync triggers BadRequest"
// during `func azure functionapp publish`).  The function body logs a warning and
// short-circuits gracefully when the value is absent.
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["SqlConnectionString"] ?? string.Empty;
    options.UseSqlServer(connectionString);
});

builder.Build().Run();
