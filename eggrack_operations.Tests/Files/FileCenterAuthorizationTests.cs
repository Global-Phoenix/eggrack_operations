using System.Reflection;
using Eggrack.Operations.Application.Modules.Files;
using Eggrack.Operations.Web.Areas.Files.Controllers;
using eggrack_operations.Areas.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eggrack.Operations.Tests.Files;

public sealed class FileCenterAuthorizationTests
{
    [Fact]
    public void FileCenterRequiresFileViewPermission()
    {
        var permission=Assert.Single(typeof(FileCenterController)
            .GetCustomAttributes<InternalPermissionAttribute>());

        Assert.Equal("files.view",Assert.Single(permission.Arguments!));
    }

    [Fact]
    public void FileCenterRequiresAuthenticatedUserAndExposesOnlyGetRoutes()
    {
        Assert.NotNull(typeof(FileCenterController).GetCustomAttribute<AuthorizeAttribute>());
        var publicActions=typeof(FileCenterController).GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.DeclaredOnly);
        Assert.All(publicActions,action=>Assert.NotEmpty(action.GetCustomAttributes<HttpGetAttribute>()));
    }

    [Theory]
    [InlineData("photo.jpg","image/jpeg",FilePreviewKind.Image)]
    [InlineData("quote.pdf","application/pdf",FilePreviewKind.Pdf)]
    [InlineData("cost.xlsx","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",FilePreviewKind.Spreadsheet)]
    [InlineData("notes.txt","text/plain",FilePreviewKind.Text)]
    public void PreviewPolicyAllowsOnlyMatchingSafeFileTypes(string name,string mimeType,FilePreviewKind kind)
    {
        var decision=FilePreviewPolicy.Resolve(name,mimeType);

        Assert.True(decision.CanInline);
        Assert.Equal(kind,decision.Kind);
    }

    [Theory]
    [InlineData("payload.svg","image/svg+xml")]
    [InlineData("page.html","text/html")]
    [InlineData("photo.jpg","text/html")]
    [InlineData("archive.zip","application/zip")]
    public void PreviewPolicyForcesActiveOrMismatchedContentToDownload(string name,string mimeType)
    {
        var decision=FilePreviewPolicy.Resolve(name,mimeType);

        Assert.False(decision.CanInline);
        Assert.Equal("application/octet-stream",decision.ContentType);
    }

    [Theory]
    [InlineData("request",12u,"/wholesale/procurement/requests/12/details")]
    [InlineData("plan",34u,"/wholesale/procurement/plans/34/details")]
    public void FileItemLinksBackToOwningBusiness(string sourceKind,uint sourceId,string expected)
    {
        var item=new FileCenterFileItem("key",sourceKind,sourceId,1,"file.pdf","application/pdf",10,
            "NO-1","客户","上传人","internal",DateTime.UtcNow,1);

        Assert.Equal(expected,item.BusinessUrl);
    }

    [Theory]
    [InlineData("plan","internal",true)]
    [InlineData("plan","customer",false)]
    [InlineData("request","internal",false)]
    public void StoredFileIdentifiesInternalPlanDocuments(string sourceKind,string visibility,bool expected)
    {
        var file=new Eggrack.Operations.Infrastructure.Modules.Files.FileCenterStoredFile(
            sourceKind,1,"file.pdf","managed/1/file.bin","application/pdf",10,visibility);

        Assert.Equal(expected,file.IsInternal);
    }
}
