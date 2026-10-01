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
        b.HasMany(x => x.Translations).WithOne().HasForeignKey(t => t.BuildingId).OnDelete(DeleteBehavior.Cascade);
        /* Every query that loads a building loads its names, so GetName() can pick the reader's language. */
        b.Navigation(x => x.Translations).AutoInclude();
        b.Property(x => x.TimeZone).IsRequired().HasMaxLength(BuildingConsts.MaxTimeZoneLength);
        b.Property(x => x.Holidays).HasColumnType("date[]");
        b.Property(x => x.ClosedWeekdays).HasColumnType("integer[]");
    }
}
