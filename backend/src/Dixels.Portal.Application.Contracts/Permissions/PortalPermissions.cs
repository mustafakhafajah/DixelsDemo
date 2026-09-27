namespace Dixels.Portal.Permissions;

public static class PortalPermissions
{
    public const string GroupName = "Portal";

    public static class Buildings
    {
        public const string Default = GroupName + ".Buildings";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Floors
    {
        public const string Default = GroupName + ".Floors";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Spaces
    {
        public const string Default = GroupName + ".Spaces";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SpaceTypes
    {
        public const string Default = GroupName + ".SpaceTypes";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Bookings
    {
        public const string Default = GroupName + ".Bookings";
        /* Act on anyone's bookings and list all bookings. */
        public const string ManageAll = Default + ".ManageAll";
    }

    /* Blocked time. Create = block time (and preview it), Delete = unblock. */
    public static class Maintenance
    {
        public const string Default = GroupName + ".Maintenance";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }
}
