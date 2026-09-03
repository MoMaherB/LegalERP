using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using LegalERP.Application.Companies;
using LegalERP.Application.Notifications;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using LegalERP.Infrastructure.Repositories;
using LegalERP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── ASP.NET Core Identity ────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ── Cookie Authentication ─────────────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;   // MUST be None for cross-origin cookie
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.LoginPath = "/api/auth/unauthorized";
    options.Events.OnRedirectToLogin = ctx =>
    {
        // Return 401 for API calls instead of redirect (Blazor handles redirect itself)
        ctx.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        // Allow any localhost origin in development so port changes don't break auth
        policy.SetIsOriginAllowed(origin =>
        {
            var uri = new Uri(origin);
            return uri.Host == "localhost" || uri.Host == "127.0.0.1";
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials(); // Required for cookies to be sent cross-origin
    });
});

// ── Hangfire ──────────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<LegalERP.Application.Cases.ICaseRepository, CaseRepository>();
builder.Services.AddScoped<LegalERP.Application.Clients.IClientRepository, ClientRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<LegalERP.Application.Storage.IFileStorageService, LegalERP.Infrastructure.Storage.LocalFileStorageService>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<WebPushNotificationService>();
builder.Services.AddScoped<HearingReminderJob>();

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.UseInlineDefinitionsForEnums();
});

var app = builder.Build();

// ── Apply Migrations and Seed Database (Roles + SuperAdmin) ───────────────────
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}
await DbSeeder.SeedAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowWebApp");
app.UseAuthentication(); // Must be before UseAuthorization
app.UseAuthorization();

// Hangfire Dashboard (admin only in production — open for dev)
app.UseHangfireDashboard("/hangfire");

app.MapControllers();

// Recurring hearing reminder job — runs daily at 08:00 AM
RecurringJob.AddOrUpdate<HearingReminderJob>(
    "hearing-reminder-daily",
    job => job.ExecuteAsync(),
    "0 8 * * *");

app.Run();