using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Spaces;

/* "Find a space" filters run in the database, and only bookable spaces ever come back. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class FindSpacesTests : PortalEntityFrameworkCoreTestBase
{
    private readonly ISpaceAppService _service;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    public FindSpacesTests()
    {
        _service = GetRequiredService<ISpaceAppService>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
    }

    [Fact]
    public async Task Only_bookable_spaces_are_returned()
    {
        var e = await CreateEstateAsync();

        var names = await FindNamesAsync(new FindSpacesInput { BuildingId = e.Building.Id });

        names.ShouldBe(new[] { $"Big {e.Tag}", $"Small {e.Tag}", $"Studio {e.Tag}" }, ignoreOrder: true);
        names.ShouldNotContain($"Closed {e.Tag}");        // the space itself is not bookable
        names.ShouldNotContain($"Upstairs {e.Tag}");      // its floor is not bookable
    }

    [Fact]
    public async Task Filters_by_floor_capacity_type_and_name()
    {
        var e = await CreateEstateAsync();

        (await FindNamesAsync(new FindSpacesInput { BuildingId = e.Building.Id, MinCapacity = 8 }))
            .ShouldBe(new[] { $"Big {e.Tag}" });
        (await FindNamesAsync(new FindSpacesInput { BuildingId = e.Building.Id, TypeIds = new List<Guid> { DefaultSpaceTypes.Studio } }))
            .ShouldBe(new[] { $"Studio {e.Tag}" });
        (await FindNamesAsync(new FindSpacesInput { BuildingId = e.Building.Id, Name = "SMALL" }))
            .ShouldBe(new[] { $"Small {e.Tag}" });
        (await FindNamesAsync(new FindSpacesInput { BuildingId = e.Building.Id, FloorName = "2" }))
            .ShouldBeEmpty();                               // floor 2 exists but is not bookable
    }

    [Fact]
    public async Task A_building_that_is_not_bookable_returns_nothing()
    {
        var e = await CreateEstateAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _buildings.GetAsync(e.Building.Id);
            b.IsBookable = false;
            await _buildings.UpdateAsync(b, autoSave: true);
        });

        (await FindNamesAsync(new FindSpacesInput { BuildingId = e.Building.Id })).ShouldBeEmpty();
    }

    private async Task<List<string>> FindNamesAsync(FindSpacesInput input)
        => (await _service.GetBookableListAsync(input)).Items.Select(s => s.Name).ToList();

    private record Estate(string Tag, Building Building);

    /* Floor 1 (bookable): Big (12 seats), Small (4), Studio (studio type), Closed (not bookable).
     * Floor 2 (not bookable): Upstairs. */
    private Task<Estate> CreateEstateAsync() => WithUnitOfWorkAsync(async () =>
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Find {tag}"), autoSave: true);
        var floor1 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
        var floor2 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "2") { IsBookable = false }, autoSave: true);

        async Task Add(string name, Floor floor, int capacity, Guid type, bool bookable = true)
            => await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"{name} {tag}", building.Id, floor.Id, type)
            {
                Capacity = capacity,
                IsBookable = bookable,
            }, autoSave: true);

        await Add("Big", floor1, 12, DefaultSpaceTypes.MeetingRoom);
        await Add("Small", floor1, 4, DefaultSpaceTypes.MeetingRoom);
        await Add("Studio", floor1, 2, DefaultSpaceTypes.Studio);
        await Add("Closed", floor1, 6, DefaultSpaceTypes.MeetingRoom, bookable: false);
        await Add("Upstairs", floor2, 6, DefaultSpaceTypes.MeetingRoom);
        return new Estate(tag, building);
    });
}
