using System;
using System.Data.Common;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dixels.Portal.Bookings;

/* The database refusing an overlapping booking (SQLSTATE 23P01) must reach the user as the ordinary
 * booking.conflict, however deeply EF wraps it; any other failure must pass through untouched.
 * (The constraint itself needs PostgreSQL; it is proven against the real database, not SQLite.) */
public class BookingOverlapTests
{
    private sealed class FakeDbException(string sqlState) : DbException("database said no")
    {
        public override string SqlState { get; } = sqlState;
    }

    /* Roughly how EF Core surfaces it: DbUpdateException wrapping the provider's exception. */
    private static Exception Wrapped(string sqlState) =>
        new InvalidOperationException("An error occurred while saving the entity changes.", new FakeDbException(sqlState));

    [Fact]
    public void An_exclusion_violation_is_recognised_even_when_wrapped()
    {
        BookingOverlap.IsOverlap(new FakeDbException(BookingOverlap.ExclusionViolation)).ShouldBeTrue();
        BookingOverlap.IsOverlap(Wrapped(BookingOverlap.ExclusionViolation)).ShouldBeTrue();
    }

    [Fact]
    public void Other_database_errors_are_not_overlaps()
    {
        BookingOverlap.IsOverlap(Wrapped("23505")).ShouldBeFalse(); // unique violation
        BookingOverlap.IsOverlap(new InvalidOperationException("nope")).ShouldBeFalse();
        BookingOverlap.IsOverlap(null).ShouldBeFalse();
    }

    [Fact]
    public async Task The_losing_save_becomes_the_normal_booking_conflict()
    {
        var ex = await Should.ThrowAsync<BookingRaceException>(() =>
            BookingOverlap.Translate(() => Task.FromException(Wrapped(BookingOverlap.ExclusionViolation))));

        ex.Code.ShouldBe(PortalDomainErrorCodes.BookingConflict);
        ex.ShouldBeAssignableTo<BusinessException>();
        ex.Data[ErrorFieldExtensions.FieldKey].ShouldBe("window");
    }

    [Fact]
    public async Task Other_failures_pass_through_unchanged()
    {
        var original = Wrapped("23505");
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => BookingOverlap.Translate(() => Task.FromException(original)));
        ex.ShouldBeSameAs(original);
    }

    [Fact]
    public async Task A_successful_save_returns_its_result()
    {
        (await BookingOverlap.Translate(() => Task.FromResult(42))).ShouldBe(42);
    }
}
