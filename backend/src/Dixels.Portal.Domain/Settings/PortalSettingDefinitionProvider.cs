using Volo.Abp.Identity.Settings;
using Volo.Abp.Settings;

namespace Dixels.Portal.Settings;

public class PortalSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(PortalSettings.Language));

        /* People may not change their own email: ABP's profile update then keeps it as it is, whatever is sent.
         * An administrator still can, on ABP's Users page. ABP's Identity settings page can switch it back on. */
        var emailUpdate = context.GetOrNull(IdentitySettingNames.User.IsEmailUpdateEnabled);
        if (emailUpdate != null)
            emailUpdate.DefaultValue = false.ToString();
    }
}
