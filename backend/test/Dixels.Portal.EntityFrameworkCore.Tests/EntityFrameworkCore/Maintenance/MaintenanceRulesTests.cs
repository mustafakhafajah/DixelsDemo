using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Spaces;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Maintenance;

/* Scope resolution and "no booking during cleaning" against a real (SQLite) database. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class MaintenanceRulesTests : PortalEntityFrameworkCoreTestBase
{
    private readonly MaintenanceScopeResolver _scopes;
    private readonly BookingManager _bookingManager;
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public MaintenanceRulesTests()
    {
        _scopes = GetRequiredService<MaintenanceScopeResolver>();
        _bookingManager = GetRequiredService<BookingManager>();
        _maintenance = GetRequiredService<IRepository<MaintenanceWindow, Guid>>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
    }

    [Fact]
    public async Task Building_scope_covers_every_space_on_every_floor()
    {
        var e = await CreateEstateAsync();

        var ids = await WithUnitOfWorkAsync(() => _scopes.GetSpaceIdsAsync(MaintenanceScopeType.Building, e.Building.Id));

        ids.ShouldBe(new[] { e.OnFloor1.Id, e.OnFloor2.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task Floor_scope_covers_only_that_floor()
    {
        var e = await CreateEstateAsync();

        var ids = await WithUnitOfWorkAsync(() => _scopes.GetSpaceIdsAsync(MaintenanceScopeType.Floor, e.Floor1.Id));

        ids.ShouldBe(new[] { e.OnFloor1.Id });
    }

    [Fact]
    public async Task A_scope_with_no_spaces_is_rejected()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _scopes.GetSpaceIdsAsync(MaintenanceScopeType.Floor, Guid.NewGuid())));
        ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceNotFound);
    }

    [Fact]
    public async Task A_booking_during_cleaning_is_rejected_but_a_cancelled_cleaning_does_not_block()
    {
        var e = await CreateEstateAsync();
        var cleaning = await WithUnitOfWorkAsync(() => _maintenance.InsertAsync(
            new MaintenanceWindow(Guid.NewGuid(), e.OnFloor1.Id, Ten, Ten.AddHours(1)), autoSave: true));

        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _bookingManager.CreateAsync(e.OnFloor1.Id, Guid.NewGuid(), Ten.AddMinutes(30), Ten.AddMinutes(90))));
        ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceUnderMaintenance);

        await WithUnitOfWorkAsync(async () =>
        {
            var m = await _maintenance.GetAsync(cleaning.Id);
            m.Status = MaintenanceStatus.Cancelled;
            await _maintenance.UpdateAsync(m, autoSave: true);
        });
        await Should.NotThrowAsync(() => WithUnitOfWorkAsync(() =>
            _bookingManager.CreateAsync(e.OnFloor1.Id, Guid.NewGuid(), Ten.AddMinutes(30), Ten.AddMinutes(90))));
    }

    private record Estate(Building Building, Floor Floor1, Space OnFloor1, Space OnFloor2);

    private Task<Estate> CreateEstateAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Test {tag}"), autoSave: true);
            var floor1 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            var floor2 = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "2"), autoSave: true);
            var a = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room 1 {tag}", building.Id, floor1.Id), autoSave: true);
            var b = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room 2 {tag}", building.Id, floor2.Id), autoSave: true);
            return new Estate(building, floor1, a, b);
        });
}
