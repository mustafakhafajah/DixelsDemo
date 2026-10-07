using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Profiles;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;

namespace Dixels.Portal;

/* Entity → DTO mappings used through ABP's ObjectMapper. Fields that need other tables (names, counts,
 * resolved rules, lifecycle) or the reader's language (translated names and notes) are ignored here and
 * filled in by the app service after mapping. */

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BuildingToBuildingDtoMapper : MapperBase<Building, BuildingDto>
{
    [MapperIgnoreTarget(nameof(BuildingDto.Name))]
    [MapperIgnoreTarget(nameof(BuildingDto.Translations))]
    [MapperIgnoreTarget(nameof(BuildingDto.FloorCount))]
    [MapperIgnoreTarget(nameof(BuildingDto.SpaceCount))]
    public override partial BuildingDto Map(Building source);

    [MapperIgnoreTarget(nameof(BuildingDto.Name))]
    [MapperIgnoreTarget(nameof(BuildingDto.Translations))]
    [MapperIgnoreTarget(nameof(BuildingDto.FloorCount))]
    [MapperIgnoreTarget(nameof(BuildingDto.SpaceCount))]
    public override partial void Map(Building source, BuildingDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FloorToFloorDtoMapper : MapperBase<Floor, FloorDto>
{
    [MapperIgnoreTarget(nameof(FloorDto.Name))]
    [MapperIgnoreTarget(nameof(FloorDto.Translations))]
    [MapperIgnoreTarget(nameof(FloorDto.BuildingName))]
    [MapperIgnoreTarget(nameof(FloorDto.SpaceCount))]
    public override partial FloorDto Map(Floor source);

    [MapperIgnoreTarget(nameof(FloorDto.Name))]
    [MapperIgnoreTarget(nameof(FloorDto.Translations))]
    [MapperIgnoreTarget(nameof(FloorDto.BuildingName))]
    [MapperIgnoreTarget(nameof(FloorDto.SpaceCount))]
    public override partial void Map(Floor source, FloorDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SpaceToSpaceDtoMapper : MapperBase<Space, SpaceDto>
{
    [MapperIgnoreTarget(nameof(SpaceDto.Name))]
    [MapperIgnoreTarget(nameof(SpaceDto.Note))]
    [MapperIgnoreTarget(nameof(SpaceDto.Translations))]
    [MapperIgnoreTarget(nameof(SpaceDto.TypeName))]
    [MapperIgnoreTarget(nameof(SpaceDto.BuildingName))]
    [MapperIgnoreTarget(nameof(SpaceDto.FloorName))]
    [MapperIgnoreTarget(nameof(SpaceDto.TimeZone))]
    [MapperIgnoreTarget(nameof(SpaceDto.Constraints))]
    [MapperIgnoreTarget(nameof(SpaceDto.CanCurrentUserBook))]
    [MapperIgnoreTarget(nameof(SpaceDto.NotBookableReason))]
    public override partial SpaceDto Map(Space source);

    [MapperIgnoreTarget(nameof(SpaceDto.Name))]
    [MapperIgnoreTarget(nameof(SpaceDto.Note))]
    [MapperIgnoreTarget(nameof(SpaceDto.Translations))]
    [MapperIgnoreTarget(nameof(SpaceDto.TypeName))]
    [MapperIgnoreTarget(nameof(SpaceDto.BuildingName))]
    [MapperIgnoreTarget(nameof(SpaceDto.FloorName))]
    [MapperIgnoreTarget(nameof(SpaceDto.TimeZone))]
    [MapperIgnoreTarget(nameof(SpaceDto.Constraints))]
    [MapperIgnoreTarget(nameof(SpaceDto.CanCurrentUserBook))]
    [MapperIgnoreTarget(nameof(SpaceDto.NotBookableReason))]
    public override partial void Map(Space source, SpaceDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class SpaceTypeToSpaceTypeDtoMapper : MapperBase<SpaceType, SpaceTypeDto>
{
    [MapperIgnoreTarget(nameof(SpaceTypeDto.Name))]
    [MapperIgnoreTarget(nameof(SpaceTypeDto.Translations))]
    [MapperIgnoreTarget(nameof(SpaceTypeDto.SpaceCount))]
    public override partial SpaceTypeDto Map(SpaceType source);

    [MapperIgnoreTarget(nameof(SpaceTypeDto.Name))]
    [MapperIgnoreTarget(nameof(SpaceTypeDto.Translations))]
    [MapperIgnoreTarget(nameof(SpaceTypeDto.SpaceCount))]
    public override partial void Map(SpaceType source, SpaceTypeDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class BookingToBookingDtoMapper : MapperBase<Booking, BookingDto>
{
    [MapperIgnoreTarget(nameof(BookingDto.SpaceName))]
    [MapperIgnoreTarget(nameof(BookingDto.OwnerName))]
    [MapperIgnoreTarget(nameof(BookingDto.Lifecycle))]
    public override partial BookingDto Map(Booking source);

    [MapperIgnoreTarget(nameof(BookingDto.SpaceName))]
    [MapperIgnoreTarget(nameof(BookingDto.OwnerName))]
    [MapperIgnoreTarget(nameof(BookingDto.Lifecycle))]
    public override partial void Map(Booking source, BookingDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class MaintenanceWindowToMaintenanceWindowDtoMapper : MapperBase<MaintenanceWindow, MaintenanceWindowDto>
{
    [MapperIgnoreTarget(nameof(MaintenanceWindowDto.SpaceName))]
    [MapperIgnoreTarget(nameof(MaintenanceWindowDto.ScopeLabel))]
    [MapperIgnoreTarget(nameof(MaintenanceWindowDto.Lifecycle))]
    public override partial MaintenanceWindowDto Map(MaintenanceWindow source);

    [MapperIgnoreTarget(nameof(MaintenanceWindowDto.SpaceName))]
    [MapperIgnoreTarget(nameof(MaintenanceWindowDto.ScopeLabel))]
    [MapperIgnoreTarget(nameof(MaintenanceWindowDto.Lifecycle))]
    public override partial void Map(MaintenanceWindow source, MaintenanceWindowDto destination);
}

/* Only what ABP's own profile leaves out; the profile itself comes from ABP's IProfileAppService. */
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class IdentityUserToMyProfileDtoMapper : MapperBase<IdentityUser, MyProfileDto>
{
    [MapperIgnoreTarget(nameof(MyProfileDto.Profile))]
    [MapperIgnoreTarget(nameof(MyProfileDto.Roles))]
    [MapperIgnoreTarget(nameof(MyProfileDto.TenantName))]
    [MapperIgnoreTarget(nameof(MyProfileDto.LastSignInTime))]
    [MapperIgnoreTarget(nameof(MyProfileDto.PictureVersion))]
    public override partial MyProfileDto Map(IdentityUser source);

    [MapperIgnoreTarget(nameof(MyProfileDto.Profile))]
    [MapperIgnoreTarget(nameof(MyProfileDto.Roles))]
    [MapperIgnoreTarget(nameof(MyProfileDto.TenantName))]
    [MapperIgnoreTarget(nameof(MyProfileDto.LastSignInTime))]
    [MapperIgnoreTarget(nameof(MyProfileDto.PictureVersion))]
    public override partial void Map(IdentityUser source, MyProfileDto destination);
}
