using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "Spaces", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.Name).IsRequired().HasMaxLength(SpaceConsts.MaxNameLength);
        b.Property(x => x.Note).HasMaxLength(SpaceConsts.MaxNoteLength);
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => new { x.BuildingId, x.FloorId });
        b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SpaceType>().WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
