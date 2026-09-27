using Dixels.Portal.SpaceTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class SpaceTypeConfiguration : IEntityTypeConfiguration<SpaceType>
{
    public void Configure(EntityTypeBuilder<SpaceType> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "SpaceTypes", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.Name).IsRequired().HasMaxLength(SpaceTypeConsts.MaxNameLength);
        b.HasIndex(x => x.Name).IsUnique();
    }
}
