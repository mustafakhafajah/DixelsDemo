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

    /* Your own bookings: Default = see them, Create = book, Edit = reschedule or end early, Delete = cancel.
     * Other people's bookings, one permission per action:
     *   ViewAll   = see everyone's bookings and who made them (lists, details, the people filter, upcoming counts);
     *   EditAll   = reschedule or end early anyone's booking (needs Edit);
     *   DeleteAll = cancel anyone's booking or series, and cancel every upcoming booking in a space / floor / building (needs Delete).
     * MultipleSpaces = may hold several spaces at the same time (an exception to the one-space-at-a-time rule; needs Create).
     * There is no CreateAll: nobody can book for someone else. */
    public static class Bookings
    {
        public const string Default = GroupName + ".Bookings";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string ViewAll = Default + ".ViewAll";
        public const string EditAll = Default + ".EditAll";
        public const string DeleteAll = Default + ".DeleteAll";
        public const string MultipleSpaces = Default + ".MultipleSpaces";
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
