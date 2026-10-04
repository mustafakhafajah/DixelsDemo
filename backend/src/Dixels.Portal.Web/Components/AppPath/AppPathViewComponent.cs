using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace Dixels.Portal.Web.Components.AppPath;

/* Tells ABP's scripts where the site lives (e.g. /server/ when IIS hosts it under a path), so the admin
 * pages call /server/api/... instead of /api/... . The theme sets no <base href>, which abp.js would read. */
public class AppPathViewComponent : AbpViewComponent
{
    public IViewComponentResult Invoke() => View("~/Components/AppPath/Default.cshtml");
}
