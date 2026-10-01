using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "Floors", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.HasMany(x => x.Translations).WithOne().HasForeignKey(t => t.FloorId).OnDelete(DeleteBehavior.Cascade);
        /* Every query that loads a floor loads its names, so GetName() can pick the reader's language. */
        b.Navigation(x => x.Translations).AutoInclude();
        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
    }
}
