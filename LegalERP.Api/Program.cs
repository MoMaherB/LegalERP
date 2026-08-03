using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using LegalERP.Application.Companies;
using LegalERP.Application.Notifications;
using LegalERP.Infrastructure.Persistence;
using LegalERP.Infrastructure.Repositories;
using LegalERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

// Repositories (Application interface -> Infrastructure implementation)
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<LegalERP.Application.Cases.ICaseRepository, CaseRepository>();
builder.Services.AddScoped<LegalERP.Application.Clients.IClientRepository, ClientRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<LegalERP.Application.Storage.IFileStorageService, LegalERP.Infrastructure.Storage.LocalFileStorageService>();

// Services
builder.Services.AddScoped<WebPushNotificationService>();
builder.Services.AddScoped<HearingReminderJob>();

// Controllers + Swagger
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

// CORS — allows LegalERP.Web (running on a different localhost port) to call this API.
// We'll tighten this to a specific origin once we know Web's exact dev URL.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowWebApp");
app.UseAuthorization();

// Hangfire Dashboard (for monitoring jobs)
app.UseHangfireDashboard("/hangfire");

app.MapControllers();

// Register the recurring hearing reminder job — runs daily at 08:00 AM
RecurringJob.AddOrUpdate<HearingReminderJob>(
    "hearing-reminder-daily",
    job => job.ExecuteAsync(),
    "0 8 * * *"); // Every day at 08:00

app.Run();