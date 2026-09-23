using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dixels.Portal.Pages;

public class Index_Tests : PortalWebTestBase
{
    [Fact]
    public async Task Welcome_Page()
    {
        var response = await GetResponseAsStringAsync("/");
        response.ShouldNotBeNull();
    }
}
