using Microsoft.AspNetCore.Builder;
using Dixels.Portal;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();

builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("Dixels.Portal.Web.csproj");
await builder.RunAbpModuleAsync<PortalWebTestModule>(applicationName: "Dixels.Portal.Web" );

public partial class Program
{
}
