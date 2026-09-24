using System;
using System.Collections.Generic;

namespace Dixels.Portal.SpaceTypes;

/* The four types that used to be a fixed enum. Their IDs are fixed so the migration can move
 * existing spaces onto them and the seeder can create them on a fresh database. */
public static class DefaultSpaceTypes
{
    public static readonly Guid MeetingRoom = new("5c0e3a1e-0b6f-4d2a-9a51-000000000001");
    public static readonly Guid Equipment = new("5c0e3a1e-0b6f-4d2a-9a51-000000000002");
    public static readonly Guid Desk = new("5c0e3a1e-0b6f-4d2a-9a51-000000000003");
    public static readonly Guid Studio = new("5c0e3a1e-0b6f-4d2a-9a51-000000000004");

    public static readonly IReadOnlyList<(Guid Id, string Name)> All = new[]
    {
        (MeetingRoom, "Meeting room"),
        (Equipment, "Equipment"),
        (Desk, "Desk"),
        (Studio, "Studio"),
    };
}
