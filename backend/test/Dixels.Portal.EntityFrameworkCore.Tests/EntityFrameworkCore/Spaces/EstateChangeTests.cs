using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Spaces;

/* Deleting estate records never leaves anything pointing at nothing: a building goes once its floors are gone,
 * a floor once its spaces are gone, and a space once nobody has a booking on it that hasn't ended. A deleted
 * record's name is free for a new one. A building's time zone must be an IANA name. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class EstateChangeTests : PortalEntityFrameworkCoreTestBase
{
    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    private readonly IBuildingAppService _buildings;
    private readonly IFloorAppService _floors;
    private readonly ISpaceAppService _spaces;
    private readonly BookingManager _bookingManager;
    private readonly IRepository<Booking, Guid> _bookings;

    public EstateChangeTests()
    {
        _buildings = GetRequiredService<IBuildingAppService>();
        _floors = GetRequiredService<IFloorAppService>();
        _spaces = GetRequiredService<ISpaceAppService>();
        _bookingManager = GetRequiredService<BookingManager>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
    }

    [Fact]
    public async Task A_building_with_floors_cannot_be_deleted()
    {
        var e = await CreateEstateAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => _buildings.DeleteAsync(e.Building.Id));
        ex.Code.ShouldBe(PortalDomainErrorCodes.BuildingNotEmpty);
    }

    [Fact]
    public async Task A_floor_with_spaces_cannot_be_deleted()
    {
        var e = await CreateEstateAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => _floors.DeleteAsync(e.Floor.Id));
        ex.Code.ShouldBe(PortalDomainErrorCodes.FloorNotEmpty);
        ex.Message.ShouldContain(e.Building.Name);
    }

    [Fact]
    public async Task A_space_with_an_upcoming_booking_cannot_be_deleted_until_it_is_cancelled()
    {
        var e = await CreateEstateAsync();
        var booking = await BookAsync(e.Space.Id);

        var ex = await Should.ThrowAsync<BusinessException>(() => _spaces.DeleteAsync(e.Space.Id));
        ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceHasBookings);

        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _bookings.GetAsync(booking.Id);
            b.Cancel();
            await _bookings.UpdateAsync(b, autoSave: true);
        });

        /* Cancelled bookings are history: now the space, then its floor, then the building can go. */
        await _spaces.DeleteAsync(e.Space.Id);
        await _floors.DeleteAsync(e.Floor.Id);
        await _buildings.DeleteAsync(e.Building.Id);
    }

    [Fact]
    public async Task A_deleted_buildings_and_spaces_names_can_be_used_again()
    {
        var e = await CreateEstateAsync();
        await _spaces.DeleteAsync(e.Space.Id);
        await _floors.DeleteAsync(e.Floor.Id);
        await _buildings.DeleteAsync(e.Building.Id);

        var again = await CreateEstateAsync(e.Tag);

        again.Building.Name.ShouldBe(e.Building.Name);
        again.Space.Name.ShouldBe(e.Space.Name);
    }

    [Theory]
    [InlineData("Arab Standard Time")]  // a Windows name: the browser would read it as UTC
    [InlineData("Mars/Base")]
    public async Task A_building_needs_an_iana_time_zone(string timeZone)
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _buildings.CreateAsync(new CreateUpdateBuildingDto
        {
            Name = $"Zone {Guid.NewGuid():N}"[..20], TimeZone = timeZone,
        }));
        ex.Code.ShouldBe(PortalDomainErrorCodes.UnknownTimeZone);
    }

    private Task<Booking> BookAsync(Guid spaceId) => WithUnitOfWorkAsync(async () =>
    {
        var booking = await _bookingManager.CreateAsync(spaceId, Guid.NewGuid(), Ten, Ten.AddHours(1));
        return await _bookings.InsertAsync(booking, autoSave: true);
    });

    private record Estate(string Tag, BuildingDto Building, FloorDto Floor, SpaceDto Space);

    /* Through the services, so names are checked the way an admin's are. */
    private async Task<Estate> CreateEstateAsync(string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];
        var building = await _buildings.CreateAsync(new CreateUpdateBuildingDto { Name = $"Gone {tag}" });
        var floor = await _floors.CreateAsync(new CreateUpdateFloorDto { BuildingId = building.Id, Name = "1" });
        var space = await _spaces.CreateAsync(new CreateUpdateSpaceDto
        {
            Name = $"Gone room {tag}", BuildingId = building.Id, FloorId = floor.Id, TypeId = DefaultSpaceTypes.MeetingRoom,
        });
        return new Estate(tag, building, floor, space);
    }
}
