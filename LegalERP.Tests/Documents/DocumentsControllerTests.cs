using System.Reflection;
using System.Text;
using FluentAssertions;
using LegalERP.Api.Controllers;
using LegalERP.Application.Common;
using LegalERP.Application.Companies;
using LegalERP.Application.Storage;
using LegalERP.Domain.Entities;
using LegalERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LegalERP.Tests.Documents;

public class DocumentsControllerTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    [Fact]
    public void DocumentOwnerTypes_DefinesCanonicalOwnerConstants()
    {
        DocumentOwnerTypes.Company.Should().Be("company");
        DocumentOwnerTypes.CompanyDoc.Should().Be("companydoc");
        DocumentOwnerTypes.Amendment.Should().Be("amendment");
        DocumentOwnerTypes.PartnerIds.Should().Be("partnerids");
        DocumentOwnerTypes.PartnerAttorneys.Should().Be("partnerattorneys");
        DocumentOwnerTypes.Client.Should().Be("client");
        DocumentOwnerTypes.Case.Should().Be("case");
        DocumentOwnerTypes.CaseMemo.Should().Be("casememo");
        DocumentOwnerTypes.CaseParty.Should().Be("caseparty");
        DocumentOwnerTypes.Attorney.Should().Be("Attorney");
        DocumentOwnerTypes.Users.Should().Be("users");
    }

    [Fact]
    public async Task Upload_WhenValidFileWithCustomTitle_SavesDocumentWithTitleAndExtension()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var mockStorage = new Mock<IFileStorageService>();
        mockStorage
            .Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "scan.pdf", DocumentOwnerTypes.CompanyDoc, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("stored-scan.pdf");

        var controller = new DocumentsController(db, mockStorage.Object);
        var formFile = CreateFormFile("scan.pdf", "application/pdf", "pdf content");

        var result = await controller.Upload(DocumentOwnerTypes.CompanyDoc, ownerId, "Tax Card", formFile, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var createdId = okResult.Value.Should().BeOfType<Guid>().Subject;

        var persisted = await db.Documents.FirstAsync(d => d.Id == createdId);
        persisted.FileName.Should().Be("Tax Card.pdf");
        persisted.StoredFileName.Should().Be("stored-scan.pdf");
        persisted.OwnerType.Should().Be(DocumentOwnerTypes.CompanyDoc);
        persisted.OwnerId.Should().Be(ownerId);
    }

    [Fact]
    public async Task Replace_WhenDocumentExists_UpdatesDocumentInPlaceAndDeletesOldStoredFile()
    {
        await using var db = CreateDbContext();
        var docId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingDoc = new Document
        {
            Id = docId,
            FileName = "old-contract.pdf",
            StoredFileName = "old-stored.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            OwnerType = DocumentOwnerTypes.Company,
            OwnerId = ownerId
        };
        db.Documents.Add(existingDoc);
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IFileStorageService>();
        mockStorage
            .Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), "new-contract.pdf", DocumentOwnerTypes.Company, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-stored.pdf");

        var controller = new DocumentsController(db, mockStorage.Object);
        var formFile = CreateFormFile("new-contract.pdf", "application/pdf", "updated pdf content");

        var result = await controller.Replace(docId, formFile, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DocumentDto>().Subject;
        dto.Id.Should().Be(docId);
        dto.FileName.Should().Be("new-contract.pdf");
        dto.StoredFileName.Should().Be("new-stored.pdf");
        dto.ContentType.Should().Be("application/pdf");
        dto.FileSizeBytes.Should().Be(formFile.Length);

        var persisted = await db.Documents.FirstAsync(d => d.Id == docId);
        persisted.FileName.Should().Be("new-contract.pdf");
        persisted.StoredFileName.Should().Be("new-stored.pdf");
        persisted.OwnerType.Should().Be(DocumentOwnerTypes.Company);
        persisted.OwnerId.Should().Be(ownerId);
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        mockStorage.Verify(
            s => s.DeleteFileAsync(DocumentOwnerTypes.Company, ownerId, "old-stored.pdf", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Replace_WhenDocumentDoesNotExist_ReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var mockStorage = new Mock<IFileStorageService>();
        var controller = new DocumentsController(db, mockStorage.Object);
        var formFile = CreateFormFile("new-contract.pdf", "application/pdf", "updated pdf content");

        var result = await controller.Replace(Guid.NewGuid(), formFile, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Replace_WhenFileExtensionIsInvalid_ReturnsBadRequest()
    {
        await using var db = CreateDbContext();
        var docId = Guid.NewGuid();
        db.Documents.Add(new Document
        {
            Id = docId,
            FileName = "old.pdf",
            StoredFileName = "old-stored.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 50,
            OwnerType = DocumentOwnerTypes.Company,
            OwnerId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IFileStorageService>();
        var controller = new DocumentsController(db, mockStorage.Object);
        var formFile = CreateFormFile("malware.exe", "application/octet-stream", "bad content");

        var result = await controller.Replace(docId, formFile, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        mockStorage.Verify(
            s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_WhenDocumentExists_SoftDeletesDocument()
    {
        await using var db = CreateDbContext();
        var docId = Guid.NewGuid();
        db.Documents.Add(new Document
        {
            Id = docId,
            FileName = "doc.pdf",
            StoredFileName = "stored.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 120,
            OwnerType = DocumentOwnerTypes.CompanyDoc,
            OwnerId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IFileStorageService>();
        var controller = new DocumentsController(db, mockStorage.Object);

        var result = await controller.Delete(docId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        var deleted = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.Id == docId);
        deleted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void DocumentsController_ActionAttributes_EnforceRbacPolicy()
    {
        var uploadMethod = typeof(DocumentsController).GetMethod(nameof(DocumentsController.Upload));
        uploadMethod.Should().NotBeNull();
        var uploadAuth = uploadMethod!.GetCustomAttribute<AuthorizeAttribute>();
        uploadAuth.Should().NotBeNull();
        uploadAuth!.Roles.Should().Be("SuperAdmin,Admin,Editor");

        var replaceMethod = typeof(DocumentsController).GetMethod(nameof(DocumentsController.Replace));
        replaceMethod.Should().NotBeNull();
        var replaceAuth = replaceMethod!.GetCustomAttribute<AuthorizeAttribute>();
        replaceAuth.Should().NotBeNull();
        replaceAuth!.Roles.Should().Be("SuperAdmin,Admin,Editor");

        var deleteMethod = typeof(DocumentsController).GetMethod(nameof(DocumentsController.Delete));
        deleteMethod.Should().NotBeNull();
        var deleteAuth = deleteMethod!.GetCustomAttribute<AuthorizeAttribute>();
        deleteAuth.Should().NotBeNull();
        deleteAuth!.Roles.Should().Be("SuperAdmin,Admin");
    }
}
