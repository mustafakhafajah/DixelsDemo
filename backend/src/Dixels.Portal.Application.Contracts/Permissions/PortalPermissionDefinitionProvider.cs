using System.Linq;
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

        /* Own bookings: the parent (see), Create, Edit, Delete. Other people's bookings: one permission per action,
         * each under the own-booking permission it widens, so ABP's permission screen ticks what the action needs. */
        var bookings = AddCrud(group, PortalPermissions.Bookings.Default, "Permission:Bookings");
        bookings.AddChild(PortalPermissions.Bookings.ViewAll, L("Permission:Bookings.ViewAll"));
        Child(bookings, PortalPermissions.Bookings.Edit).AddChild(PortalPermissions.Bookings.EditAll, L("Permission:Bookings.EditAll"));
        Child(bookings, PortalPermissions.Bookings.Delete).AddChild(PortalPermissions.Bookings.DeleteAll, L("Permission:Bookings.DeleteAll"));
        Child(bookings, PortalPermissions.Bookings.Create).AddChild(PortalPermissions.Bookings.MultipleSpaces, L("Permission:Bookings.MultipleSpaces"));
    }

    private static PermissionDefinition Child(PermissionDefinition parent, string name)
        => parent.Children.Single(c => c.Name == name);

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
