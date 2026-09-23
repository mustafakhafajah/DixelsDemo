using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Dixels.Portal.Estate;

/* Seeds the mock's sample estate (teams, 2 buildings, 5 floors, 6 spaces). Bookings are not
 * seeded: fixed dates would go stale, and the UI creates real ones. */
public class EstateDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Team, Guid> _teams;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IGuidGenerator _guids;

    public EstateDataSeedContributor(IRepository<Team, Guid> teams, IRepository<Building, Guid> buildings,
        IRepository<Floor, Guid> floors, IRepository<Space, Guid> spaces, IGuidGenerator guids)
    {
        _teams = teams;
        _buildings = buildings;
        _floors = floors;
        _spaces = spaces;
        _guids = guids;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        var teams = await SeedTeamsAsync();
        if (!await _buildings.AnyAsync())
            await SeedEstateAsync(teams);
    }

    private async Task<Dictionary<string, Guid>> SeedTeamsAsync()
    {
        if (!await _teams.AnyAsync())
        {
            foreach (var name in new[] { "Platform", "Design", "Finance", "Field Ops" })
                await _teams.InsertAsync(new Team(_guids.Create(), name), autoSave: true);
        }
        return (await _teams.GetListAsync()).ToDictionary(t => t.Name, t => t.Id);
    }

    private async Task SeedEstateAsync(Dictionary<string, Guid> teams)
    {
        var hq = await _buildings.InsertAsync(new Building(_guids.Create(), "HQ North") { TimeZone = "UTC" }, autoSave: true);
        var annex = await _buildings.InsertAsync(new Building(_guids.Create(), "Annex") { TimeZone = "Europe/Warsaw" }, autoSave: true);

        var floors = new Dictionary<string, Floor>();
        foreach (var (b, name) in new[] { (hq, "2"), (hq, "3"), (hq, "4"), (annex, "1"), (annex, "2") })
            floors[$"{b.Name}|{name}"] = await _floors.InsertAsync(new Floor(_guids.Create(), b.Id, name), autoSave: true);

        async Task AddSpace(string name, SpaceType type, Building b, string floor, int capacity, string note,
            string[] teamNames, EstateStatus status = EstateStatus.Active)
        {
            await _spaces.InsertAsync(new Space(_guids.Create(), name, b.Id, floors[$"{b.Name}|{floor}"].Id)
            {
                Type = type,
                Status = status,
                TimeZone = b.TimeZone,
                Capacity = capacity,
                Note = note,
                RestrictedTeamIds = teamNames.Select(t => teams[t]).ToList(),
            }, autoSave: true);
        }

        await AddSpace("Conference Room A", SpaceType.MeetingRoom, hq, "3", 12, "Seats 12 · projector fixed to the room", Array.Empty<string>());
        await AddSpace("Conference Room B", SpaceType.MeetingRoom, hq, "3", 6, "Seats 6 · whiteboard wall", new[] { "Platform", "Design" });
        await AddSpace("Annex Meeting Pod", SpaceType.MeetingRoom, annex, "1", 4, "Seats 4 · quiet booth", Array.Empty<string>());
        await AddSpace("AV Cart 01", SpaceType.Equipment, hq, "2", 0, "Portable · return to floor 2", Array.Empty<string>());
        await AddSpace("Video Kit 02", SpaceType.Equipment, annex, "1", 0, "Camera, tripod, two mics", new[] { "Field Ops" });
        await AddSpace("Usability Lab", SpaceType.MeetingRoom, hq, "4", 8, "Out of service for rewiring", Array.Empty<string>(), EstateStatus.Inactive);
    }
}
