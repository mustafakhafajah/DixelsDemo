using Dixels.Portal.Emailing;
using Volo.Abp.Emailing;
using Volo.Abp.Settings;

namespace Dixels.Portal.Settings;

public class ExchangeOnlineSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        /* Not encrypted: the values come from the server's appsettings.secrets.json, which ABP reads as written
         * (an encrypted setting would expect an encrypted value there). */
        context.Add(
            new SettingDefinition(ExchangeOnlineSettingNames.Enabled, "false"),
            new SettingDefinition(ExchangeOnlineSettingNames.AuthType, nameof(ExchangeAuthType.OAuth2)),
            new SettingDefinition(ExchangeOnlineSettingNames.TenantId),
            new SettingDefinition(ExchangeOnlineSettingNames.ClientId),
            new SettingDefinition(ExchangeOnlineSettingNames.ClientSecret),
            new SettingDefinition(ExchangeOnlineSettingNames.ServiceMailbox),
            new SettingDefinition(ExchangeOnlineSettingNames.AllowedFromAddresses)
        );

        /* ABP's own mail settings default to a local server on port 25. Point them at Exchange Online's client
         * submission endpoint (STARTTLS on 587) instead; any of them can still be overridden, as Docker does for Mailpit. */
        SetDefault(context, EmailSettingNames.Smtp.Host, "smtp.office365.com");
        SetDefault(context, EmailSettingNames.Smtp.Port, "587");
        SetDefault(context, EmailSettingNames.Smtp.EnableSsl, "true");
        SetDefault(context, EmailSettingNames.DefaultFromDisplayName, "Dixels Portal");
    }

    private static void SetDefault(ISettingDefinitionContext context, string name, string value)
    {
        var definition = context.GetOrNull(name);
        if (definition != null) definition.DefaultValue = value;
    }
}
