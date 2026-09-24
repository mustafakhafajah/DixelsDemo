using Dixels.Portal.Buildings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "Buildings", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.Name).IsRequired().HasMaxLength(BuildingConsts.MaxNameLength);
        b.Property(x => x.TimeZone).IsRequired().HasMaxLength(BuildingConsts.MaxTimeZoneLength);
        b.Property(x => x.Holidays).HasColumnType("date[]");
        b.HasIndex(x => x.Name).IsUnique();
    }
}
