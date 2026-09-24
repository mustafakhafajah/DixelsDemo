namespace Dixels.Portal.Permissions;

public static class PortalPermissions
{
    public const string GroupName = "Portal";

    public static class Buildings
    {
        public const string Default = GroupName + ".Buildings";
        public const string Manage = Default + ".Manage";
    }

    public static class Floors
    {
        public const string Default = GroupName + ".Floors";
        public const string Manage = Default + ".Manage";
    }

    public static class Spaces
    {
        public const string Default = GroupName + ".Spaces";
        public const string Manage = Default + ".Manage";
    }

    public static class Bookings
    {
        public const string Default = GroupName + ".Bookings";
        /* Act on anyone's bookings, list all bookings, reserve parking. */
        public const string ManageAll = Default + ".ManageAll";
    }

    public static class Maintenance
    {
        public const string Default = GroupName + ".Maintenance";
        public const string Manage = Default + ".Manage";
    }
}
