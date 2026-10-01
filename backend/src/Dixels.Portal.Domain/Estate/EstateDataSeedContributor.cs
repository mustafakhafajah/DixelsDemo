using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace Dixels.Portal.Estate;

/* Seeds the default space types, then the mock's sample estate (2 buildings, 5 floors, 6 spaces).
 * Bookings are not seeded: fixed dates would go stale, and the UI creates real ones. */
public class EstateDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<SpaceType, Guid> _spaceTypes;
    private readonly IGuidGenerator _guids;

    public EstateDataSeedContributor(IRepository<Building, Guid> buildings, IRepository<Floor, Guid> floors,
        IRepository<Space, Guid> spaces, IRepository<SpaceType, Guid> spaceTypes, IGuidGenerator guids)
    {
        _buildings = buildings;
        _floors = floors;
        _spaces = spaces;
        _spaceTypes = spaceTypes;
        _guids = guids;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await SeedDefaultSpaceTypesAsync();
        if (!await _buildings.AnyAsync())
            await SeedEstateAsync();
    }

    /* Only adds a default type, or a language of one, that is missing, so renaming one in the admin page
     * survives re-seeding. */
    private async Task SeedDefaultSpaceTypesAsync()
    {
        foreach (var (id, names) in DefaultSpaceTypes.All)
        {
            var type = await _spaceTypes.FindAsync(id);
            if (type == null)
            {
                type = new SpaceType(id, names[PortalLanguages.Default]);
                foreach (var (language, name) in names) type.SetName(language, name);
                await _spaceTypes.InsertAsync(type, autoSave: true);
                continue;
            }
            var missing = names.Where(n => type.FindTranslation(n.Key) == null).ToList();
            if (missing.Count == 0) continue;
            foreach (var (language, name) in missing) type.SetName(language, name);
            await _spaceTypes.UpdateAsync(type, autoSave: true);
        }
    }

    /* Sample data is in English only; admins add other languages in the edit forms. */
    private async Task SeedEstateAsync()
    {
        const string en = PortalLanguages.Default;
        var hq = await _buildings.InsertAsync(new Building(_guids.Create(), "HQ North") { TimeZone = "UTC" }, autoSave: true);
        var annex = await _buildings.InsertAsync(new Building(_guids.Create(), "Annex") { TimeZone = "Europe/Warsaw" }, autoSave: true);

        var floors = new Dictionary<string, Floor>();
        foreach (var (b, name) in new[] { (hq, "2"), (hq, "3"), (hq, "4"), (annex, "1"), (annex, "2") })
            floors[$"{b.GetName(en)}|{name}"] = await _floors.InsertAsync(new Floor(_guids.Create(), b.Id, name), autoSave: true);

        async Task AddSpace(string name, Guid typeId, Building b, string floor, int capacity, string note,
            bool isBookable = true)
        {
            await _spaces.InsertAsync(new Space(_guids.Create(), name, b.Id, floors[$"{b.GetName(en)}|{floor}"].Id, typeId, note)
            {
                IsBookable = isBookable,
                Capacity = capacity,
            }, autoSave: true);
        }

        await AddSpace("Conference Room A", DefaultSpaceTypes.MeetingRoom, hq, "3", 12, "Seats 12 · projector fixed to the room");
        await AddSpace("Conference Room B", DefaultSpaceTypes.MeetingRoom, hq, "3", 6, "Seats 6 · whiteboard wall");
        await AddSpace("Annex Meeting Pod", DefaultSpaceTypes.MeetingRoom, annex, "1", 4, "Seats 4 · quiet booth");
        await AddSpace("AV Cart 01", DefaultSpaceTypes.Equipment, hq, "2", 0, "Portable · return to floor 2");
        await AddSpace("Video Kit 02", DefaultSpaceTypes.Equipment, annex, "1", 0, "Camera, tripod, two mics");
        await AddSpace("Usability Lab", DefaultSpaceTypes.MeetingRoom, hq, "4", 8, "Out of service for rewiring", isBookable: false);
    }
}
