using Volo.Abp.Settings;

namespace Dixels.Portal.Settings;

public class PortalSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(PortalSettings.Language));
    }
}
