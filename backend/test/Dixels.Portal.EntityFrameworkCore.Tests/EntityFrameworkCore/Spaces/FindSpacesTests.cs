using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
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
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly BookingManager _bookingManager;

    public FindSpacesTests()
    {
        _service = GetRequiredService<ISpaceAppService>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
        _maintenance = GetRequiredService<IRepository<MaintenanceWindow, Guid>>();
        _bookingManager = GetRequiredService<BookingManager>();
    }

    [Fact]
    public async Task Only_bookable_spaces_are_returned()
    {
        var e = await CreateEstateAsync();

        var names = await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id });

        names.ShouldBe(new[] { $"Big {e.Tag}", $"Small {e.Tag}", $"Studio {e.Tag}" }, ignoreOrder: true);
        names.ShouldNotContain($"Closed {e.Tag}");        // the space itself is not bookable
        names.ShouldNotContain($"Upstairs {e.Tag}");      // its floor is not bookable
    }

    [Fact]
    public async Task Filters_by_floor_capacity_type_and_name()
    {
        var e = await CreateEstateAsync();

        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, MinCapacity = 8 }))
            .ShouldBe(new[] { $"Big {e.Tag}" });
        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, TypeIds = new List<Guid> { DefaultSpaceTypes.Studio } }))
            .ShouldBe(new[] { $"Studio {e.Tag}" });
        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, Name = "SMALL" }))
            .ShouldBe(new[] { $"Small {e.Tag}" });
        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, FloorId = e.Floor1.Id }))
            .ShouldBe(new[] { $"Big {e.Tag}", $"Small {e.Tag}", $"Studio {e.Tag}" }, ignoreOrder: true);
        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, FloorId = e.Floor2.Id }))
            .ShouldBeEmpty();                               // floor 2 exists but is not bookable
    }

    /* "Free only": a booking or blocked time in the window, or a window outside opening hours, drops the space. */
    [Fact]
    public async Task Free_only_keeps_the_spaces_that_could_be_booked_for_the_window()
    {
        var e = await CreateEstateAsync();
        var ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);
        await WithUnitOfWorkAsync(async () =>
        {
            var booking = await _bookingManager.CreateAsync(e.Big.Id, Guid.NewGuid(), ten, ten.AddHours(1));
            await _bookings.InsertAsync(booking, autoSave: true);
            await _maintenance.InsertAsync(new MaintenanceWindow(Guid.NewGuid(), e.Small.Id, ten.AddMinutes(30), ten.AddHours(2)), autoSave: true);
        });

        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, FreeFromUtc = ten, FreeToUtc = ten.AddHours(1) }))
            .ShouldBe(new[] { $"Studio {e.Tag}" });
        /* Touching is not overlapping: Big is free again from 11:00, Small is still blocked until 12:00. */
        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, FreeFromUtc = ten.AddHours(1), FreeToUtc = ten.AddHours(2) }))
            .ShouldBe(new[] { $"Big {e.Tag}", $"Studio {e.Tag}" }, ignoreOrder: true);
        /* 03:00-04:00 is outside every space's opening hours. */
        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id, FreeFromUtc = ten.AddHours(-7), FreeToUtc = ten.AddHours(-6) }))
            .ShouldBeEmpty();
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

        (await FindNamesAsync(new GetSpaceListInput { BuildingId = e.Building.Id })).ShouldBeEmpty();
    }

    /* The same collection without the bookable filter is the registry: every space, bookable or not. */
    [Fact]
    public async Task Bookable_false_returns_only_the_spaces_that_cannot_be_booked()
    {
        var e = await CreateEstateAsync();

        var notBookable = await _service.GetListAsync(new GetSpaceListInput { BuildingId = e.Building.Id, Bookable = false });
        var every = await _service.GetListAsync(new GetSpaceListInput { BuildingId = e.Building.Id });

        /* Closed is switched off itself; Upstairs is on a floor that is switched off. */
        notBookable.Items.Select(s => s.Name).ShouldBe(new[] { $"Closed {e.Tag}", $"Upstairs {e.Tag}" }, ignoreOrder: true);
        every.TotalCount.ShouldBe(5);
    }

    /* "Find a space" asks the space collection for bookable spaces only. */
    private async Task<List<string>> FindNamesAsync(GetSpaceListInput input)
    {
        input.Bookable = true;
        return (await _service.GetListAsync(input)).Items.Select(s => s.Name).ToList();
    }

    private record Estate(string Tag, Building Building, Floor Floor1, Floor Floor2, Space Big, Space Small);

    /* Floor 1 (bookable): Big (12 seats), Small (4), Studio (studio type), Closed (not bookable).
     * Floor 2 (not bookable): Upstairs. */
    private Task<Estate> CreateEstateAsync() => WithUnitOfWorkAsync(async () =>
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Find {tag}"), autoSave: true);
        var floor1 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
        var floor2 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "2") { IsBookable = false }, autoSave: true);

        async Task<Space> Add(string name, Floor floor, int capacity, Guid type, bool bookable = true)
            => await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"{name} {tag}", building.Id, floor.Id, type)
            {
                Capacity = capacity,
                IsBookable = bookable,
            }, autoSave: true);

        var big = await Add("Big", floor1, 12, DefaultSpaceTypes.MeetingRoom);
        var small = await Add("Small", floor1, 4, DefaultSpaceTypes.MeetingRoom);
        await Add("Studio", floor1, 2, DefaultSpaceTypes.Studio);
        await Add("Closed", floor1, 6, DefaultSpaceTypes.MeetingRoom, bookable: false);
        await Add("Upstairs", floor2, 6, DefaultSpaceTypes.MeetingRoom);
        return new Estate(tag, building, floor1, floor2, big, small);
    });
}
