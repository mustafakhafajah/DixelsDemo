using Dixels.Portal.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Dixels.Portal.Web.Pages;

/* Inherit your PageModel classes from this class.
 */
public abstract class PortalPageModel : AbpPageModel
{
    protected PortalPageModel()
    {
        LocalizationResourceType = typeof(PortalResource);
    }
}
