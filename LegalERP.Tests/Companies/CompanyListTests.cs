using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Domain.Enums;
using LegalERP.Web;
using LegalERP.Web.Components.Pages.Companies;
using LegalERP.Web.Services;
using LegalERP.Web.Services.Toast;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace LegalERP.Tests.Companies;

public class CompanyListTests : TestContext
{
    private readonly Mock<IToastService> _mockToast = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _mockLoc = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();
    private readonly FakeCompanyListHttpMessageHandler _httpHandler = new();

    public CompanyListTests()
    {
        _mockLoc.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));

        var client = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost/") };
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("TestAdmin");
        auth.SetRoles("SuperAdmin");

        Services.AddSingleton(_mockToast.Object);
        Services.AddSingleton(_mockLoc.Object);
        Services.AddSingleton(new CompanyApiClient(_mockHttpFactory.Object));
    }

    [Fact]
    public void CompanyList_WhenLoaded_RendersCompanyTableRows()
    {
        var compId = Guid.NewGuid();
        _httpHandler.Companies =
        [
            CreateCompanyDto(
                id: compId,
                fileNumber: "COMP-001",
                category: CompanyCategory.SoleProprietorship,
                companyName: "شركة الأهرام للتجارة",
                companyNameEn: "Al-Ahram Trading",
                tradeName: "الأهرام",
                establishmentDate: new DateOnly(2022, 5, 10),
                registrationNumber: "987654",
                address: "القاهرة"
            )
        ];

        var cut = RenderComponent<CompanyList>();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(1);
            var text = rows[0].TextContent;
            text.Should().Contain("COMP-001");
            text.Should().Contain("شركة الأهرام للتجارة");
            text.Should().Contain("الأهرام");
            text.Should().Contain("SoleProprietorship");
        });
    }

    [Fact]
    public void CompanyList_WhenEmpty_RendersNoCompaniesMatchAlert()
    {
        _httpHandler.Companies = [];

        var cut = RenderComponent<CompanyList>();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".alert-info").TextContent.Should().Contain("NoCompaniesMatch");
        });
    }

    [Fact]
    public async Task CompanyList_Search_SendsTermAndUpdatesTable()
    {
        _httpHandler.Companies =
        [
            CreateCompanyDto(Guid.NewGuid(), companyName: "شركة قديمة")
        ];
        _httpHandler.SearchResults =
        [
            CreateCompanyDto(Guid.NewGuid(), companyName: "شركة الفرات الحديثة", category: CompanyCategory.Partnership)
        ];

        var cut = RenderComponent<CompanyList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("الفرات");

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("الفرات");
            cut.Find("tbody tr").TextContent.Should().Contain("شركة الفرات الحديثة");
        });
    }

    [Fact]
    public async Task CompanyList_CategoryFilter_SendsCategoryToSearchApi()
    {
        _httpHandler.Companies = [];
        _httpHandler.SearchResults =
        [
            CreateCompanyDto(Guid.NewGuid(), companyName: "شركة مساهمة كبرى", category: CompanyCategory.CapitalCompany)
        ];

        var cut = RenderComponent<CompanyList>();

        var select = cut.Find("select.ios-select");
        select.Change(CompanyCategory.CapitalCompany.ToString());

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastCategory.Should().Be(CompanyCategory.CapitalCompany.ToString());
            cut.Find("tbody tr").TextContent.Should().Contain("شركة مساهمة كبرى");
        });
    }

    [Fact]
    public async Task CompanyList_DeleteCompany_WhenConfirmed_CallsDeleteApiShowsToastAndRefreshes()
    {
        var compId = Guid.NewGuid();
        _httpHandler.Companies =
        [
            CreateCompanyDto(compId, companyName: "شركة للحذف")
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = RenderComponent<CompanyList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().Be(compId);
        _mockToast.Verify(t => t.ShowSuccess("CompanyDeleted"), Times.Once);
    }

    [Fact]
    public async Task CompanyList_DeleteCompany_WhenCancelled_DoesNotCallDeleteApi()
    {
        var compId = Guid.NewGuid();
        _httpHandler.Companies =
        [
            CreateCompanyDto(compId, companyName: "شركة للإبقاء")
        ];

        JSInterop.Setup<bool>("confirm", _ => true).SetResult(false);

        var cut = RenderComponent<CompanyList>();

        var deleteBtn = cut.WaitForElement("button.ios-btn-danger");
        await cut.InvokeAsync(() => deleteBtn.Click());

        _httpHandler.LastDeletedId.Should().BeNull();
        _mockToast.Verify(t => t.ShowSuccess(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CompanyList_WithPagination_RendersControlsAndNavigatesBetweenPages()
    {
        _httpHandler.Companies = Enumerable.Range(1, 35)
            .Select(i => CreateCompanyDto(Guid.NewGuid(), fileNumber: $"COMP-{i:D2}", companyName: $"شركة رقم {i}"))
            .ToList();

        var cut = RenderComponent<CompanyList>();

        // Page 1: 25 items rendered
        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(25);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 2");

            var prevBtn = cut.Find("button.pagination-prev");
            prevBtn.HasAttribute("disabled").Should().BeTrue();

            var nextBtn = cut.Find("button.pagination-next");
            nextBtn.HasAttribute("disabled").Should().BeFalse();
        });

        // Click next -> Page 2: 10 items rendered
        var nextButton = cut.Find("button.pagination-next");
        await cut.InvokeAsync(() => nextButton.Click());

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(10);
            rows[0].TextContent.Should().Contain("COMP-26");
            cut.Find(".pagination-indicator").TextContent.Should().Contain("2 / 2");

            var prevBtn = cut.Find("button.pagination-prev");
            prevBtn.HasAttribute("disabled").Should().BeFalse();

            var nextBtn = cut.Find("button.pagination-next");
            nextBtn.HasAttribute("disabled").Should().BeTrue();
        });

        // Click previous -> Page 1 again
        var prevButton = cut.Find("button.pagination-prev");
        await cut.InvokeAsync(() => prevButton.Click());

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tbody tr");
            rows.Should().HaveCount(25);
            rows[0].TextContent.Should().Contain("COMP-01");
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 2");
        });
    }

    [Fact]
    public async Task CompanyList_WhenPageSizeChanged_UpdatesPageAndItemsCount()
    {
        _httpHandler.Companies = Enumerable.Range(1, 25)
            .Select(i => CreateCompanyDto(Guid.NewGuid(), fileNumber: $"COMP-{i:D2}", companyName: $"شركة {i}"))
            .ToList();

        var cut = RenderComponent<CompanyList>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(25);
        });

        // Change page size to 10
        var select = cut.Find("select.ios-select.form-select-sm");
        await cut.InvokeAsync(() => select.Change("10"));

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(10);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 3");
        });
    }

    [Fact]
    public async Task CompanyList_PreservesFiltersAcrossPagination()
    {
        _httpHandler.SearchResults = Enumerable.Range(1, 30)
            .Select(i => CreateCompanyDto(Guid.NewGuid(), fileNumber: $"COMP-F-{i:D2}", companyName: $"شركة حديثة {i}", category: CompanyCategory.Partnership))
            .ToList();

        var cut = RenderComponent<CompanyList>();

        var searchInput = cut.WaitForElement("input.ios-input");
        searchInput.Input("حديثة");

        var select = cut.Find("select.ios-select");
        select.Change(CompanyCategory.Partnership.ToString());

        var searchBtn = cut.FindAll("button.ios-btn-primary")
            .First(b => b.TextContent.Contains("Search"));
        await cut.InvokeAsync(() => searchBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("حديثة");
            _httpHandler.LastCategory.Should().Be(CompanyCategory.Partnership.ToString());
            cut.FindAll("tbody tr").Should().HaveCount(25);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 2");
        });

        // Click next on filtered results
        var nextBtn = cut.Find("button.pagination-next");
        await cut.InvokeAsync(() => nextBtn.Click());

        cut.WaitForAssertion(() =>
        {
            _httpHandler.LastSearchTerm.Should().Be("حديثة");
            _httpHandler.LastCategory.Should().Be(CompanyCategory.Partnership.ToString());
            cut.FindAll("tbody tr").Should().HaveCount(5);
            cut.Find("tbody tr").TextContent.Should().Contain("COMP-F-26");
            cut.Find(".pagination-indicator").TextContent.Should().Contain("2 / 2");
        });
    }

    [Fact]
    public void CompanyList_WhenOutOfRangePage_PreviousAndNextButtonsRemainDisabled()
    {
        _httpHandler.Companies = Enumerable.Range(1, 5)
            .Select(i => CreateCompanyDto(Guid.NewGuid(), companyName: $"شركة {i}"))
            .ToList();

        var cut = RenderComponent<CompanyList>();

        cut.WaitForAssertion(() =>
        {
            cut.FindAll("tbody tr").Should().HaveCount(5);
            cut.Find(".pagination-indicator").TextContent.Should().Contain("1 / 1");
            cut.Find("button.pagination-prev").HasAttribute("disabled").Should().BeTrue();
            cut.Find("button.pagination-next").HasAttribute("disabled").Should().BeTrue();
        });
    }

    private static CompanyDto CreateCompanyDto(
        Guid id,
        string? fileNumber = null,
        CompanyCategory category = CompanyCategory.SoleProprietorship,
        string companyName = "شركة اختبار",
        string? companyNameEn = null,
        string? tradeName = null,
        DateOnly? establishmentDate = null,
        string? registrationNumber = null,
        string? address = null,
        Guid? incorporationDocumentId = null,
        DocumentDto? incorporationDocument = null,
        bool hasManyPartners = false,
        Guid? partnersIdsDocumentId = null,
        Guid? partnersAttorneysDocumentId = null)
    {
        return new CompanyDto(
            id,
            fileNumber,
            category,
            companyName,
            companyNameEn,
            tradeName,
            establishmentDate,
            registrationNumber,
            address,
            incorporationDocumentId,
            incorporationDocument,
            hasManyPartners,
            null,
            partnersIdsDocumentId,
            null,
            partnersAttorneysDocumentId,
            null,
            [],
            []
        );
    }

    private sealed class FakeCompanyListHttpMessageHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        public List<CompanyDto> Companies { get; set; } = [];
        public List<CompanyDto> SearchResults { get; set; } = [];
        public string? LastSearchTerm { get; private set; }
        public string? LastCategory { get; private set; }
        public Guid? LastDeletedId { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            if (request.Method == HttpMethod.Get && path.Contains("/api/companies/search"))
            {
                var uri = request.RequestUri!;
                var queryParams = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.OrdinalIgnoreCase);

                queryParams.TryGetValue("term", out var term);
                LastSearchTerm = term;

                queryParams.TryGetValue("category", out var cat);
                LastCategory = cat;

                int pageNumber = queryParams.TryGetValue("pageNumber", out var pageStr) && int.TryParse(pageStr, out var parsedPage) ? parsedPage : 1;
                int pageSize = queryParams.TryGetValue("pageSize", out var sizeStr) && int.TryParse(sizeStr, out var parsedSize) ? parsedSize : 25;

                var source = (string.IsNullOrWhiteSpace(term) && string.IsNullOrWhiteSpace(cat)) ? Companies : SearchResults;

                if (path.Contains("/paged") || queryParams.ContainsKey("pageNumber"))
                {
                    var pagedItems = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, new PagedResult<CompanyDto>(pagedItems, source.Count, pageNumber, pageSize)));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, source));
            }

            if (request.Method == HttpMethod.Get && (path.Contains("/api/companies/paged") || path.Contains("/api/companies")))
            {
                var uri = request.RequestUri!;
                var queryParams = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Split('=', 2))
                    .Where(p => p.Length == 2)
                    .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]), StringComparer.OrdinalIgnoreCase);

                int pageNumber = queryParams.TryGetValue("pageNumber", out var pageStr) && int.TryParse(pageStr, out var parsedPage) ? parsedPage : 1;
                int pageSize = queryParams.TryGetValue("pageSize", out var sizeStr) && int.TryParse(sizeStr, out var parsedSize) ? parsedSize : 25;

                if (path.Contains("/paged") || queryParams.ContainsKey("pageNumber"))
                {
                    var pagedItems = Companies.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, new PagedResult<CompanyDto>(pagedItems, Companies.Count, pageNumber, pageSize)));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.OK, Companies));
            }

            if (request.Method == HttpMethod.Delete && path.Contains("/api/companies/"))
            {
                var parts = path.Split('?')[0].Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && Guid.TryParse(parts[^1], out var id))
                {
                    LastDeletedId = id;
                    Companies = Companies.Where(c => c.Id != id).ToList();
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T payload)
        {
            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };
        }
    }
}
