using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TravelRequestWF.Infrastructure.Data;
using TravelRequestWF.Infrastructure.Identity;
using TravelRequestWF.Infrastructure.Services;
using TravelRequestWF.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// AzureStorage options
builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection("AzureStorage"));

// Application services
builder.Services.AddHttpClient<PowerAutomateNotificationService>();
builder.Services.AddScoped<INotificationService, PowerAutomateNotificationService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<ITravelRequestService, TravelRequestService>();

var app = builder.Build();

// Seed roles and test users at startup (idempotent).
// Wrapped in try-catch: a transient DB failure during seeding must not crash the host
// and trigger an Azure App Service restart loop.  The seeder is safe to retry on next
// cold-start; core functionality (login, pages) is unaffected if seeding is skipped once.
using (var scope = app.Services.CreateScope())
{
    var seederLogger = scope.ServiceProvider
        .GetRequiredService<ILogger<Program>>();
    try
    {
        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        seederLogger.LogWarning(ex,
            "IdentitySeeder failed on this startup — likely a transient DB connectivity issue. " +
            "The app will continue; seeding will be retried on the next cold-start.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
