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
        b.HasMany(x => x.Translations).WithOne().HasForeignKey(t => t.SpaceTypeId).OnDelete(DeleteBehavior.Cascade);
        /* Every query that loads a space type loads its names, so GetName() can pick the reader's language. */
        b.Navigation(x => x.Translations).AutoInclude();
    }
}
