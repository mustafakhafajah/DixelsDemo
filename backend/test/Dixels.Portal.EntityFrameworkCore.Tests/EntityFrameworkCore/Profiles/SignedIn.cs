using System;
using System.Collections.Generic;
using System.Security.Claims;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace Dixels.Portal.EntityFrameworkCore.Profiles;

/* Makes calls inside the using block as the given user would: their id (and tenant) in the claims. */
internal static class SignedIn
{
    public static IDisposable As(this ICurrentPrincipalAccessor principal, IdentityUser user)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, user.Id.ToString()),
            new(AbpClaimTypes.UserName, user.UserName),
        };
        if (user.TenantId.HasValue)
            claims.Add(new Claim(AbpClaimTypes.TenantId, user.TenantId.Value.ToString()));

        return principal.Change(new ClaimsPrincipal(new ClaimsIdentity(claims)));
    }
}
