using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using TravelRequestWF.Infrastructure.Data;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddOpenTelemetry()
    .UseFunctionsWorkerDefaults()
    .UseAzureMonitorExporter();

var connectionString = builder.Configuration["SqlConnectionString"]
    ?? throw new InvalidOperationException("SqlConnectionString is not configured. Set it in local.settings.json (local) or Application Settings (Azure).");

builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Build().Run();
