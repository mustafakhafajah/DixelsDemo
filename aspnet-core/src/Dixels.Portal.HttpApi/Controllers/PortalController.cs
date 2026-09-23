using Dixels.Portal.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dixels.Portal.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class PortalController : AbpControllerBase
{
    protected PortalController()
    {
        LocalizationResource = typeof(PortalResource);
    }
}
