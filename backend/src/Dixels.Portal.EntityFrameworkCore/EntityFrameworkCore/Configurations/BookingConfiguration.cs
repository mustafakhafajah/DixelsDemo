using Dixels.Portal.Bookings;
using Dixels.Portal.Spaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "Bookings", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.IdempotencyKey).HasMaxLength(BookingConsts.MaxIdempotencyKeyLength);
        b.HasIndex(x => new { x.SpaceId, x.Status, x.StartUtc, x.EndUtc });
        b.HasIndex(x => new { x.OwnerUserId, x.Status, x.StartUtc, x.EndUtc });
        b.HasIndex(x => x.SeriesId);
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
        b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
