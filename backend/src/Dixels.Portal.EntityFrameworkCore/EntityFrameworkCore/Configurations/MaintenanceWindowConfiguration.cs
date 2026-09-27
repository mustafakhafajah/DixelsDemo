using Dixels.Portal.Maintenance;
using Dixels.Portal.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class MaintenanceWindowConfiguration : IEntityTypeConfiguration<MaintenanceWindow>
{
    public void Configure(EntityTypeBuilder<MaintenanceWindow> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "MaintenanceWindows", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.Note).HasMaxLength(MaintenanceWindowConsts.MaxNoteLength);
        b.HasIndex(x => new { x.SpaceId, x.Status, x.StartUtc, x.EndUtc });
        b.HasIndex(x => x.SeriesId);
        b.HasIndex(x => new { x.ScopeType, x.ScopeId });
        b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
