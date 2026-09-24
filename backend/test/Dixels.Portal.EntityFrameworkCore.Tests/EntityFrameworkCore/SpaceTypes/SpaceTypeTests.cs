using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.SpaceTypes;

/* The admin-managed type list, and the rule that a space always uses its building's time zone. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class SpaceTypeTests : PortalEntityFrameworkCoreTestBase
{
    private readonly ISpaceTypeAppService _types;
    private readonly ISpaceAppService _spaces;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public SpaceTypeTests()
    {
        _types = GetRequiredService<ISpaceTypeAppService>();
        _spaces = GetRequiredService<ISpaceAppService>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
    }

    [Fact]
    public async Task The_four_default_types_are_seeded()
    {
        var names = (await _types.GetListAsync()).Items.Select(t => t.Name).ToList();

        names.ShouldContain("Meeting room");
        names.ShouldContain("Equipment");
        names.ShouldContain("Desk");
        names.ShouldContain("Studio");
    }

    [Fact]
    public async Task A_new_type_can_be_added_renamed_and_given_to_a_space()
    {
        var booth = await _types.CreateAsync(new CreateUpdateSpaceTypeDto { Name = $"Phone booth {Tag()}" });
        var renamed = await _types.UpdateAsync(booth.Id, new CreateUpdateSpaceTypeDto { Name = $"Call booth {Tag()}" });
        var (building, floor) = await CreateBuildingAsync("UTC");

        var space = await _spaces.CreateAsync(NewSpace(building, floor, booth.Id));

        space.TypeId.ShouldBe(booth.Id);
        space.TypeName.ShouldBe(renamed.Name);
        (await _types.GetListAsync()).Items.Single(t => t.Id == booth.Id).SpaceCount.ShouldBe(1);
    }

    [Fact]
    public async Task Type_names_are_unique_ignoring_case()
    {
        var name = $"Lounge {Tag()}";
        await _types.CreateAsync(new CreateUpdateSpaceTypeDto { Name = name });

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _types.CreateAsync(new CreateUpdateSpaceTypeDto { Name = name.ToUpperInvariant() }));
        ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceTypeDuplicate);
    }

    [Fact]
    public async Task A_type_in_use_cannot_be_deleted_but_an_unused_one_can()
    {
        var used = await _types.CreateAsync(new CreateUpdateSpaceTypeDto { Name = $"Used {Tag()}" });
        var unused = await _types.CreateAsync(new CreateUpdateSpaceTypeDto { Name = $"Unused {Tag()}" });
        var (building, floor) = await CreateBuildingAsync("UTC");
        await _spaces.CreateAsync(NewSpace(building, floor, used.Id));

        var ex = await Should.ThrowAsync<BusinessException>(() => _types.DeleteAsync(used.Id));
        ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceTypeInUse);

        await _types.DeleteAsync(unused.Id);
        (await _types.GetListAsync()).Items.ShouldNotContain(t => t.Id == unused.Id);
    }

    [Fact]
    public async Task A_space_needs_an_existing_type()
    {
        var (building, floor) = await CreateBuildingAsync("UTC");

        var ex = await Should.ThrowAsync<BusinessException>(() => _spaces.CreateAsync(NewSpace(building, floor, Guid.NewGuid())));
        ex.Code.ShouldBe(PortalDomainErrorCodes.InvalidSpaceType);
    }

    [Fact]
    public async Task A_space_always_shows_its_building_time_zone_even_after_the_building_changes()
    {
        var (building, floor) = await CreateBuildingAsync("Europe/Warsaw");
        var space = await _spaces.CreateAsync(NewSpace(building, floor, DefaultSpaceTypes.Desk));
        space.TimeZone.ShouldBe("Europe/Warsaw");

        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _buildings.GetAsync(building.Id);
            b.TimeZone = "Asia/Dubai";
            await _buildings.UpdateAsync(b, autoSave: true);
        });

        (await _spaces.GetAsync(space.Id)).TimeZone.ShouldBe("Asia/Dubai");
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static CreateUpdateSpaceDto NewSpace(Building building, Floor floor, Guid typeId) => new()
    {
        Name = $"Room {Tag()}",
        TypeId = typeId,
        Status = EstateStatus.Active,
        BuildingId = building.Id,
        FloorId = floor.Id,
    };

    private Task<(Building, Floor)> CreateBuildingAsync(string timeZone)
        => WithUnitOfWorkAsync(async () =>
        {
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Test {Tag()}") { TimeZone = timeZone }, autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            return (building, floor);
        });
}
