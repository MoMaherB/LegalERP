using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Auth;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Auth;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Auth;

public class UserManagementTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeUserManagementHttpMessageHandler _httpHandler = new();

    public UserManagementTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("SuperAdminUser");
        auth.SetRoles("SuperAdmin");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new UserApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void UserManagement_WhenLoaded_RendersUserTableWithRolesAndStatusBadges()
    {
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        _httpHandler.Users =
        [
            new UserDto(user1Id, "أحمد المدير", "ahmed@erp.com", "SuperAdmin", true, "avatar.png", DateTime.UtcNow),
            new UserDto(user2Id, "سالم المحرر", "salem@erp.com", "Editor", false, null, DateTime.UtcNow)
        ];

        var cut = RenderComponent<UserManagement>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(2);

            // User 1
            rows[0].TextContent.Should().Contain("أحمد المدير");
            rows[0].TextContent.Should().Contain("ahmed@erp.com");
            rows[0].TextContent.Should().Contain("مدير عام");
            rows[0].QuerySelector(".status-pill")?.TextContent.Should().Contain("نشط");

            // User 2
            rows[1].TextContent.Should().Contain("سالم المحرر");
            rows[1].TextContent.Should().Contain("salem@erp.com");
            rows[1].TextContent.Should().Contain("محرر");
            rows[1].QuerySelector(".status-pill")?.TextContent.Should().Contain("معطّل");

            // Footer
            cut.Find(".um-footer").TextContent.Should().Contain("2");
            cut.Find(".um-footer").TextContent.Should().Contain("1 نشط");
            cut.Find(".um-footer").TextContent.Should().Contain("1 معطّل");
        });
    }

    [Fact]
    public void UserManagement_WhenEmpty_RendersNoUsersFoundState()
    {
        _httpHandler.Users = [];

        var cut = RenderComponent<UserManagement>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".um-card").TextContent.Should().Contain("لا يوجد مستخدمون حتى الآن.");
        });
    }

    [Fact]
    public async Task UserManagement_DeactivateUser_WhenConfirmed_CallsDeactivateApiShowsSuccessAndReloads()
    {
        var userId = Guid.NewGuid();
        _httpHandler.Users =
        [
            new UserDto(userId, "مستخدم للتعطيل", "deact@erp.com", "Editor", true, null, DateTime.UtcNow)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<UserManagement>();

        var deactBtn = cut.WaitForElement("button.act-warn");
        await cut.InvokeAsync(() => deactBtn.Click());

        _httpHandler.LastDeactivatedId.Should().Be(userId);
        _mockToast.Verify(t => t.ShowSuccess("تم تعطيل الحساب."), Times.Once);

        cut.WaitForAssertion(() =>
        {
            cut.Find(".status-pill").TextContent.Should().Contain("معطّل");
        });
    }

    [Fact]
    public async Task UserManagement_DeactivateUser_WhenCancelled_DoesNotCallDeactivateApi()
    {
        var userId = Guid.NewGuid();
        _httpHandler.Users =
        [
            new UserDto(userId, "مستخدم للإبقاء", "keep@erp.com", "Editor", true, null, DateTime.UtcNow)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var cut = RenderComponent<UserManagement>();

        var deactBtn = cut.WaitForElement("button.act-warn");
        await cut.InvokeAsync(() => deactBtn.Click());

        _httpHandler.LastDeactivatedId.Should().BeNull();
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UserManagement_ReactivateUser_WhenConfirmed_CallsReactivateApiShowsSuccessAndReloads()
    {
        var userId = Guid.NewGuid();
        _httpHandler.Users =
        [
            new UserDto(userId, "مستخدم للتفعيل", "react@erp.com", "Editor", false, null, DateTime.UtcNow)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<UserManagement>();

        var reactBtn = cut.WaitForElement("button.act-ok");
        await cut.InvokeAsync(() => reactBtn.Click());

        _httpHandler.LastReactivatedId.Should().Be(userId);
        _mockToast.Verify(t => t.ShowSuccess("تم إعادة تفعيل الحساب."), Times.Once);

        cut.WaitForAssertion(() =>
        {
            cut.Find(".status-pill").TextContent.Should().Contain("نشط");
        });
    }

    [Fact]
    public async Task UserManagement_PermanentDelete_WhenConfirmed_CallsDeletePermanentApiShowsSuccessAndReloads()
    {
        var userId = Guid.NewGuid();
        _httpHandler.Users =
        [
            new UserDto(userId, "مستخدم للحذف", "delete@erp.com", "Viewer", true, null, DateTime.UtcNow)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<UserManagement>();

        var delBtn = cut.WaitForElement("button.act-del");
        await cut.InvokeAsync(() => delBtn.Click());

        _httpHandler.LastPermanentDeletedId.Should().Be(userId);
        _mockToast.Verify(t => t.ShowSuccess("تم حذف الحساب نهائياً."), Times.Once);

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task UserManagement_PermanentDelete_WhenCancelled_DoesNotCallDeletePermanentApi()
    {
        var userId = Guid.NewGuid();
        _httpHandler.Users =
        [
            new UserDto(userId, "مستخدم آمن", "safe@erp.com", "Viewer", true, null, DateTime.UtcNow)
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var cut = RenderComponent<UserManagement>();

        var delBtn = cut.WaitForElement("button.act-del");
        await cut.InvokeAsync(() => delBtn.Click());

        _httpHandler.LastPermanentDeletedId.Should().BeNull();
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    private sealed class FakeUserManagementHttpMessageHandler : HttpMessageHandler
    {
        public List<UserDto> Users { get; set; } = [];
        public Guid? LastDeactivatedId { get; private set; }
        public Guid? LastReactivatedId { get; private set; }
        public Guid? LastPermanentDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Equals("/api/users", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Users));
            }

            if (request.Method == HttpMethod.Delete && path.EndsWith("/permanent", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && Guid.TryParse(segments[^2], out var id))
                {
                    LastPermanentDeletedId = id;
                    Users = Users.Where(u => u.Id != id).ToList();
                }
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, new { message = "تم حذف الحساب نهائياً." }));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/reactivate", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && Guid.TryParse(segments[^2], out var id))
                {
                    LastReactivatedId = id;
                    var user = Users.FirstOrDefault(u => u.Id == id);
                    if (user != null)
                    {
                        var idx = Users.IndexOf(user);
                        Users[idx] = user with { IsActive = true };
                    }
                }
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, new { message = "تم إعادة تفعيل الحساب بنجاح." }));
            }

            if (request.Method == HttpMethod.Delete && path.StartsWith("/api/users/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (Guid.TryParse(segments.Last(), out var id))
                {
                    LastDeactivatedId = id;
                    var user = Users.FirstOrDefault(u => u.Id == id);
                    if (user != null)
                    {
                        var idx = Users.IndexOf(user);
                        Users[idx] = user with { IsActive = false };
                    }
                }
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, new { message = "تم تعطيل الحساب بنجاح." }));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
            };
        }
    }
}
