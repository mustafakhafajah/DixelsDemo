using Dixels.Portal.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dixels.Portal.Permissions;

public class PortalPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(PortalPermissions.GroupName, L("Permission:Portal"));

        group.AddPermission(PortalPermissions.Teams.Default, L("Permission:Teams"))
            .AddChild(PortalPermissions.Teams.Manage, L("Permission:Manage"));
        group.AddPermission(PortalPermissions.Buildings.Default, L("Permission:Buildings"))
            .AddChild(PortalPermissions.Buildings.Manage, L("Permission:Manage"));
        group.AddPermission(PortalPermissions.Floors.Default, L("Permission:Floors"))
            .AddChild(PortalPermissions.Floors.Manage, L("Permission:Manage"));
        group.AddPermission(PortalPermissions.Spaces.Default, L("Permission:Spaces"))
            .AddChild(PortalPermissions.Spaces.Manage, L("Permission:Manage"));
        group.AddPermission(PortalPermissions.Bookings.Default, L("Permission:Bookings"))
            .AddChild(PortalPermissions.Bookings.ManageAll, L("Permission:ManageAll"));
        group.AddPermission(PortalPermissions.Maintenance.Default, L("Permission:Maintenance"))
            .AddChild(PortalPermissions.Maintenance.Manage, L("Permission:Manage"));
        group.AddPermission(PortalPermissions.ActivityLog.Default, L("Permission:ActivityLog"))
            .AddChild(PortalPermissions.ActivityLog.ViewAll, L("Permission:ViewAll"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<PortalResource>(name);
    }
}
