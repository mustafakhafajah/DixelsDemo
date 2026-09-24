using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Spaces;

/* The admin registry end to end: filters, paging and upcoming counts through the real app service and EF. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class SpaceRegistryTests : PortalEntityFrameworkCoreTestBase
{
    private readonly ISpaceAppService _service;
    private readonly BookingManager _bookingManager;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public SpaceRegistryTests()
    {
        _service = GetRequiredService<ISpaceAppService>();
        _bookingManager = GetRequiredService<BookingManager>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
    }

    [Fact]
    public async Task Pages_through_the_results_in_name_order_with_the_total()
    {
        var e = await CreateEstateAsync(spacesPerFloor: 12);

        var first = await _service.GetPagedListAsync(new GetSpacesInput { BuildingId = e.Building.Id, MaxResultCount = 10 });
        var last = await _service.GetPagedListAsync(new GetSpacesInput { BuildingId = e.Building.Id, SkipCount = 20, MaxResultCount = 10 });

        first.TotalCount.ShouldBe(24);
        first.Items.Count.ShouldBe(10);
        last.Items.Count.ShouldBe(4);
        first.Items.Select(s => s.Name).ShouldBe(first.Items.Select(s => s.Name).OrderBy(n => n));
        first.Items.Select(s => s.Id).Intersect(last.Items.Select(s => s.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Page_size_is_capped_at_100()
    {
        var e = await CreateEstateAsync(spacesPerFloor: 60);

        var page = await _service.GetPagedListAsync(new GetSpacesInput { BuildingId = e.Building.Id, MaxResultCount = 1000 });

        page.TotalCount.ShouldBe(120);
        page.Items.Count.ShouldBe(100);
    }

    [Fact]
    public async Task Filters_by_floor_name_and_type()
    {
        var e = await CreateEstateAsync(spacesPerFloor: 3);
        await WithUnitOfWorkAsync(async () =>
        {
            var s = await _spaces.GetAsync(e.Floor2Spaces[0].Id);
            s.TypeId = DefaultSpaceTypes.Studio;
            await _spaces.UpdateAsync(s, autoSave: true);
        });

        (await _service.GetPagedListAsync(new GetSpacesInput { FloorId = e.Floor2.Id })).TotalCount.ShouldBe(3);
        (await _service.GetPagedListAsync(new GetSpacesInput { BuildingId = e.Building.Id, Name = $"ROOM 2-{e.Tag}" }))
            .TotalCount.ShouldBe(3);
        var studios = await _service.GetPagedListAsync(new GetSpacesInput { BuildingId = e.Building.Id, TypeId = DefaultSpaceTypes.Studio });
        studios.Items.Single().Id.ShouldBe(e.Floor2Spaces[0].Id);
    }

    [Theory]
    [InlineData("SP-{0}")]      // exactly as the UI shows it
    [InlineData("sp-{0}")]      // lower-case
    [InlineData("{0}")]         // without the prefix
    public async Task Finds_a_space_by_its_displayed_id(string pattern)
    {
        var e = await CreateEstateAsync(spacesPerFloor: 2);
        var target = e.Floor1Spaces[1];
        var shortId = target.Id.ToString("N")[..8].ToUpperInvariant();

        var page = await _service.GetPagedListAsync(new GetSpacesInput { Code = string.Format(pattern, shortId) });

        page.Items.Select(s => s.Id).ShouldContain(target.Id);
        page.Items.ShouldAllBe(s => s.Id.ToString().StartsWith(shortId.ToLowerInvariant()));
    }

    [Fact]
    public async Task Finds_a_space_by_its_full_guid()
    {
        var e = await CreateEstateAsync(spacesPerFloor: 2);
        var target = e.Floor1Spaces[0];

        var page = await _service.GetPagedListAsync(new GetSpacesInput { Code = target.Id.ToString().ToUpperInvariant() });

        page.Items.Single().Id.ShouldBe(target.Id);
    }

    [Fact]
    public async Task Counts_only_confirmed_future_bookings_for_the_spaces_on_the_page()
    {
        var e = await CreateEstateAsync(spacesPerFloor: 2);
        var busy = e.Floor1Spaces[0];
        await BookAsync(busy, 0, 1);
        await BookAsync(busy, 2, 3);
        var cancelled = await BookAsync(busy, 4, 5);
        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _bookings.GetAsync(cancelled.Id);
            b.Status = BookingStatus.Cancelled;
            await _bookings.UpdateAsync(b, autoSave: true);
        });

        var page = await _service.GetPagedListAsync(new GetSpacesInput { BuildingId = e.Building.Id });

        page.UpcomingBookingCounts[busy.Id].ShouldBe(2);
        page.UpcomingBookingCounts.ContainsKey(e.Floor1Spaces[1].Id).ShouldBeFalse();
        page.UpcomingBookingCounts.Keys.ShouldAllBe(id => page.Items.Any(s => s.Id == id));
    }

    [Fact]
    public void The_id_filter_also_translates_for_PostgreSQL()
    {
        /* Production runs on Postgres, tests on SQLite: build the query for Npgsql (no connection needed). */
        using var db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql("Host=localhost;Database=translation-check").Options);

        /* IgnoreQueryFilters: ABP's soft-delete filter needs ABP's services, which a hand-made context lacks. */
        var sql = db.Set<Space>().IgnoreQueryFilters().ApplyRegistryFilter(new GetSpacesInput { Code = "SP-3F2A", Name = "room" }).ToQueryString();

        sql.ShouldContain("::text");
        sql.ShouldContain("LIKE");
    }

    private Task<Booking> BookAsync(Space space, double fromHours, double toHours)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _bookingManager.CreateAsync(space.Id, Guid.NewGuid(), Ten.AddHours(fromHours), Ten.AddHours(toHours));
            return await _bookings.InsertAsync(booking, autoSave: true);
        });

    private record Estate(string Tag, Building Building, Floor Floor1, Floor Floor2, List<Space> Floor1Spaces, List<Space> Floor2Spaces);

    /* Each test gets its own building, so seed data and other tests never affect the counts. */
    private Task<Estate> CreateEstateAsync(int spacesPerFloor)
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Test {tag}"), autoSave: true);
            var floor1 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            var floor2 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "2"), autoSave: true);
            async Task<List<Space>> Add(Floor f, int n)
            {
                var list = new List<Space>();
                for (var i = 0; i < n; i++)
                    list.Add(await _spaces.InsertAsync(
                        new Space(Guid.NewGuid(), $"Room {f.Name}-{tag}-{i:00}", building.Id, f.Id, DefaultSpaceTypes.MeetingRoom),
                        autoSave: true));
                return list;
            }
            return new Estate(tag, building, floor1, floor2, await Add(floor1, spacesPerFloor), await Add(floor2, spacesPerFloor));
        });
}
