using LegalERP.Web.Components;
using LegalERP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient("LegalErpApi", client =>
{
    // Must match whatever port LegalERP.Api runs on — check its
    // launchSettings.json or the URL Swagger opened at (Step 5e).
    client.BaseAddress = new Uri("https://localhost:7148/");
});
builder.Services.AddScoped<CompanyApiClient>();
builder.Services.AddScoped<CaseApiClient>();
builder.Services.AddScoped<ClientApiClient>();

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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
