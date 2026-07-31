using System.Text.Json.Serialization;
using LegalERP.Application.Companies;
using LegalERP.Infrastructure.Persistence;
using LegalERP.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories (Application interface -> Infrastructure implementation)
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<LegalERP.Application.Cases.ICaseRepository, CaseRepository>();
builder.Services.AddScoped<LegalERP.Application.Clients.IClientRepository, ClientRepository>();
builder.Services.AddScoped<LegalERP.Application.Storage.IFileStorageService, LegalERP.Infrastructure.Storage.LocalFileStorageService>();

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
app.MapControllers();

app.Run();