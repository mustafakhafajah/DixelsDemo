using Dixels.Portal.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dixels.Portal.Permissions;

public class PortalPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(PortalPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(PortalPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<PortalResource>(name);
    }
}
