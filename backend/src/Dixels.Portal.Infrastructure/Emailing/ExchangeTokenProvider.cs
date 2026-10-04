using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Emailing;

/* Access tokens for SMTP on Exchange Online, using the app registration's client secret (client credentials flow).
 * MSAL caches each token inside its client-application object until shortly before it expires, so one object is
 * kept per tenant/app/secret for the life of the process; a changed secret simply gets a new one. */
public class ExchangeTokenProvider : ISingletonDependency
{
    /* ".default" = the permissions granted to the app in Entra ID, i.e. SMTP.SendAsApp. */
    public static readonly string[] Scopes = ["https://outlook.office365.com/.default"];

    private readonly ConcurrentDictionary<string, IConfidentialClientApplication> _apps = new();

    public async Task<string> GetAccessTokenAsync(string tenantId, string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        var app = _apps.GetOrAdd(CacheKey(tenantId, clientId, clientSecret), _ =>
            ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .Build());

        var result = await app.AcquireTokenForClient(Scopes).ExecuteAsync(cancellationToken);
        return result.AccessToken;
    }

    /* The secret is hashed so it isn't kept around as a dictionary key. */
    private static string CacheKey(string tenantId, string clientId, string clientSecret) =>
        $"{tenantId}|{clientId}|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)))}";
}
