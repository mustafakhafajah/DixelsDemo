using Dixels.Portal.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.Portal.EntityFrameworkCore.Configurations;

public class BookingAttendeeConfiguration : IEntityTypeConfiguration<BookingAttendee>
{
    public void Configure(EntityTypeBuilder<BookingAttendee> b)
    {
        b.ToTable(PortalConsts.DbTablePrefix + "BookingAttendees", PortalConsts.DbSchema);
        b.ConfigureByConvention();
        b.Property(x => x.Email).HasMaxLength(BookingConsts.MaxAttendeeEmailLength);
        /* "Bookings I'm invited to" looks people up by user. */
        b.HasIndex(x => x.UserId);
        /* Nobody is on one booking twice; a forgotten guest (no user, no email) can be there many times. */
        b.HasIndex(x => new { x.BookingId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        b.HasIndex(x => new { x.BookingId, x.Email }).IsUnique().HasFilter("\"Email\" IS NOT NULL");
        /* A guest's answer link finds their row by the hash of its secret. */
        b.Property(x => x.ResponseTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.ResponseTokenHash).IsUnique().HasFilter("\"ResponseTokenHash\" IS NOT NULL");
        b.HasOne<Booking>().WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
