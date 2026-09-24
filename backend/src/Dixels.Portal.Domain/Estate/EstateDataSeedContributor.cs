using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Dixels.Portal.Estate;

/* Seeds the mock's sample estate (2 buildings, 5 floors, 6 spaces). Bookings are not
 * seeded: fixed dates would go stale, and the UI creates real ones. */
public class EstateDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IGuidGenerator _guids;

    public EstateDataSeedContributor(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
        IRepository<Space, Guid> spaces, IGuidGenerator guids)
    {
        _buildings = buildings;
        _floors = floors;
        _spaces = spaces;
        _guids = guids;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        if (!await _buildings.AnyAsync())
            await SeedEstateAsync();
    }

    private async Task SeedEstateAsync()
    {
        var hq = await _buildings.InsertAsync(new Building(_guids.Create(), "HQ North") { TimeZone = "UTC" }, autoSave: true);
        var annex = await _buildings.InsertAsync(new Building(_guids.Create(), "Annex") { TimeZone = "Europe/Warsaw" }, autoSave: true);

        var floors = new Dictionary<string, Floor>();
        foreach (var (b, name) in new[] { (hq, "2"), (hq, "3"), (hq, "4"), (annex, "1"), (annex, "2") })
            floors[$"{b.Name}|{name}"] = await _floors.InsertAsync(new Floor(_guids.Create(), b.Id, name), autoSave: true);

        async Task AddSpace(string name, SpaceType type, Building b, string floor, int capacity, string note,
            EstateStatus status = EstateStatus.Active)
        {
            await _spaces.InsertAsync(new Space(_guids.Create(), name, b.Id, floors[$"{b.Name}|{floor}"].Id)
            {
                Type = type,
                Status = status,
                TimeZone = b.TimeZone,
                Capacity = capacity,
                Note = note,
            }, autoSave: true);
        }

        await AddSpace("Conference Room A", SpaceType.MeetingRoom, hq, "3", 12, "Seats 12 · projector fixed to the room");
        await AddSpace("Conference Room B", SpaceType.MeetingRoom, hq, "3", 6, "Seats 6 · whiteboard wall");
        await AddSpace("Annex Meeting Pod", SpaceType.MeetingRoom, annex, "1", 4, "Seats 4 · quiet booth");
        await AddSpace("AV Cart 01", SpaceType.Equipment, hq, "2", 0, "Portable · return to floor 2");
        await AddSpace("Video Kit 02", SpaceType.Equipment, annex, "1", 0, "Camera, tripod, two mics");
        await AddSpace("Usability Lab", SpaceType.MeetingRoom, hq, "4", 8, "Out of service for rewiring", EstateStatus.Inactive);
    }
}
