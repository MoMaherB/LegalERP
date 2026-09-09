using LegalERP.Web.Components;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor ────────────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddLocalization();

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    });
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>(sp =>
    (CustomAuthStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

// ── HTTP Client ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("LegalErpApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7148/");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Allow cookies to be sent with API requests (needed for auth cookie)
    UseCookies = true,
    AllowAutoRedirect = false,
    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
});

// ── App Services ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<CompanyApiClient>();
builder.Services.AddScoped<CaseApiClient>();
builder.Services.AddScoped<ClientApiClient>();
builder.Services.AddScoped<NotificationApiClient>();
builder.Services.AddScoped<FinancialsApiClient>();
builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<UserApiClient>();
builder.Services.AddScoped<AttorneyApiClient>();
builder.Services.AddScoped<IToastService, ToastService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

var supportedCultures = new[] { "ar-EG", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar-EG")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders.Remove(
    localizationOptions.RequestCultureProviders
        .FirstOrDefault(p => p is Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider));

app.UseRequestLocalization(localizationOptions);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
