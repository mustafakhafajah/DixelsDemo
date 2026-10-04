using Dixels.Portal.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal;

/* Domain services whose messages reach the user: L looks the sentence up in Localization/Portal/{language}.json
 * for the request's language. The error code stays the same in every language. */
public abstract class PortalDomainService : DomainService
{
    protected IStringLocalizer L => LazyServiceProvider.LazyGetRequiredService<IStringLocalizer<PortalResource>>();
}
