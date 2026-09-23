using System;
using System.Collections.Generic;
using System.Text;
using Dixels.Portal.Localization;
using Volo.Abp.Application.Services;

namespace Dixels.Portal;

/* Inherit your application services from this class.
 */
public abstract class PortalAppService : ApplicationService
{
    protected PortalAppService()
    {
        LocalizationResource = typeof(PortalResource);
    }
}
