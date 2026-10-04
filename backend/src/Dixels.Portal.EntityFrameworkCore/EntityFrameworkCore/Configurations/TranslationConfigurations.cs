using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Localization;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

/* One row per entity per language, keyed by (owner, language). Names are unique within a language;
 * the same name in two languages is fine. */

public class BuildingTranslationConfiguration : IEntityTypeConfiguration<BuildingTranslation>
{
    public void Configure(EntityTypeBuilder<BuildingTranslation> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "BuildingTranslations", PortalConsts.DbSchema);
        b.HasKey(x => new { x.BuildingId, x.Language });
        b.Property(x => x.Language).HasMaxLength(PortalLanguages.MaxCodeLength);
        b.Property(x => x.Name).IsRequired().HasMaxLength(BuildingConsts.MaxNameLength);
        b.HasIndex(x => new { x.Language, x.Name }).IsUnique();
    }
}

/* Floor names are unique within their building and language; FloorManager checks that, as the building
 * is not on this table. */
public class FloorTranslationConfiguration : IEntityTypeConfiguration<FloorTranslation>
{
    public void Configure(EntityTypeBuilder<FloorTranslation> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "FloorTranslations", PortalConsts.DbSchema);
        b.HasKey(x => new { x.FloorId, x.Language });
        b.Property(x => x.Language).HasMaxLength(PortalLanguages.MaxCodeLength);
        b.Property(x => x.Name).IsRequired().HasMaxLength(FloorConsts.MaxNameLength);
    }
}

public class SpaceTypeTranslationConfiguration : IEntityTypeConfiguration<SpaceTypeTranslation>
{
    public void Configure(EntityTypeBuilder<SpaceTypeTranslation> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "SpaceTypeTranslations", PortalConsts.DbSchema);
        b.HasKey(x => new { x.SpaceTypeId, x.Language });
        b.Property(x => x.Language).HasMaxLength(PortalLanguages.MaxCodeLength);
        b.Property(x => x.Name).IsRequired().HasMaxLength(SpaceTypeConsts.MaxNameLength);
        b.HasIndex(x => new { x.Language, x.Name }).IsUnique();
    }
}

public class SpaceTranslationConfiguration : IEntityTypeConfiguration<SpaceTranslation>
{
    public void Configure(EntityTypeBuilder<SpaceTranslation> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "SpaceTranslations", PortalConsts.DbSchema);
        b.HasKey(x => new { x.SpaceId, x.Language });
        b.Property(x => x.Language).HasMaxLength(PortalLanguages.MaxCodeLength);
        b.Property(x => x.Name).IsRequired().HasMaxLength(SpaceConsts.MaxNameLength);
        b.Property(x => x.Note).HasMaxLength(SpaceConsts.MaxNoteLength);
        b.HasIndex(x => new { x.Language, x.Name }).IsUnique();
    }
}
