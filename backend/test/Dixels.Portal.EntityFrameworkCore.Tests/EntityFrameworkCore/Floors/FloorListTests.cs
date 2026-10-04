using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Floors;

/* The Floors page asks the server for one building's floors, one page at a time, in floor-number order. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class FloorListTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IFloorAppService _service;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public FloorListTests()
    {
        _service = GetRequiredService<IFloorAppService>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
    }

    [Fact]
    public async Task Filters_by_building_and_pages_in_floor_number_order()
    {
        var (building, other) = await WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var b = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Floors {tag}"), autoSave: true);
            var o = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Other {tag}"), autoSave: true);
            foreach (var name in new[] { "10", "1", "2" })
                await _floors.InsertAsync(new Floor(Guid.NewGuid(), b.Id, name), autoSave: true);
            await _floors.InsertAsync(new Floor(Guid.NewGuid(), o.Id, "1"), autoSave: true);
            return (b, o);
        });

        var all = await _service.GetListAsync(new GetFloorListInput { BuildingId = building.Id });
        var second = await _service.GetListAsync(new GetFloorListInput { BuildingId = building.Id, SkipCount = 1, MaxResultCount = 1 });

        all.TotalCount.ShouldBe(3);
        all.Items.Select(f => f.Name).ShouldBe(new[] { "1", "2", "10" });
        second.TotalCount.ShouldBe(3);
        second.Items.Single().Name.ShouldBe("2");
        all.Items.ShouldAllBe(f => f.BuildingId == building.Id);
        (await _service.GetListAsync(new GetFloorListInput { BuildingId = other.Id })).TotalCount.ShouldBe(1);
    }
}
