using Dixels.Portal.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dixels.Portal.Permissions;

public class PortalPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(PortalPermissions.GroupName, L("Permission:Portal"));

        AddCrud(group, PortalPermissions.Buildings.Default, "Permission:Buildings");
        AddCrud(group, PortalPermissions.Floors.Default, "Permission:Floors");
        AddCrud(group, PortalPermissions.Spaces.Default, "Permission:Spaces");
        AddCrud(group, PortalPermissions.SpaceTypes.Default, "Permission:SpaceTypes");
        AddCrud(group, PortalPermissions.Maintenance.Default, "Permission:Maintenance");

        AddCrud(group, PortalPermissions.Bookings.Default, "Permission:Bookings")
            .AddChild(PortalPermissions.Bookings.ManageAll, L("Permission:ManageAll"));
    }

    /* The ABP convention: a parent permission to view, with Create / Edit / Delete children. */
    private static PermissionDefinition AddCrud(PermissionGroupDefinition group, string name, string displayName)
    {
        var parent = group.AddPermission(name, L(displayName));
        parent.AddChild(name + ".Create", L("Permission:Create"));
        parent.AddChild(name + ".Edit", L("Permission:Edit"));
        parent.AddChild(name + ".Delete", L("Permission:Delete"));
        return parent;
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<PortalResource>(name);
    }
}
