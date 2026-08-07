using LegalERP.Web.Components;
using LegalERP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddLocalization();

builder.Services.AddHttpClient("LegalErpApi", client =>
{
    // Must match whatever port LegalERP.Api runs on — check its
    // launchSettings.json or the URL Swagger opened at (Step 5e).
    client.BaseAddress = new Uri("https://localhost:7148/");
});
builder.Services.AddScoped<CompanyApiClient>();
builder.Services.AddScoped<CaseApiClient>();
builder.Services.AddScoped<ClientApiClient>();
builder.Services.AddScoped<NotificationApiClient>();
builder.Services.AddScoped<FinancialsApiClient>();
builder.Services.AddScoped<MockAuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

var supportedCultures = new[] { "ar-EG", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar-EG")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

// Clear providers to ignore the browser's Accept-Language header.
// It will only use the Cookie provider and the Query string provider.
localizationOptions.RequestCultureProviders.Remove(
    localizationOptions.RequestCultureProviders
        .FirstOrDefault(p => p is Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider));

app.UseRequestLocalization(localizationOptions);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
