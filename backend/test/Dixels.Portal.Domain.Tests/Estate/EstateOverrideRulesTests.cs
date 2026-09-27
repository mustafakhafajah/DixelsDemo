using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dixels.Portal.Estate;

/* EstateOverrideRules is pure, so like ConstraintResolver it needs no database or ABP base class. */
public class EstateOverrideRulesTests
{
    private static readonly ResolvedBounds Parent = new(OpenHour: 8, CloseHour: 20, MinBookingMinutes: 15, MaxBookingHours: 8);

    private static void Check(int? open = null, int? close = null, int? min = null, int? max = null)
        => EstateOverrideRules.EnsureOnlyNarrows("Floor", "the building", "the building's", Parent, open, close, min, max);

    [Fact]
    public void No_overrides_is_always_allowed() => Should.NotThrow(() => Check());

    [Fact]
    public void Narrowing_every_field_is_allowed() => Should.NotThrow(() => Check(open: 9, close: 18, min: 30, max: 4));

    [Theory]
    [InlineData(7, null, null, null)]   // opens earlier
    [InlineData(null, 21, null, null)]  // closes later
    [InlineData(null, null, 10, null)]  // shorter minimum
    [InlineData(null, null, null, 9)]   // longer maximum
    public void Widening_any_field_is_rejected(int? open, int? close, int? min, int? max)
    {
        var ex = Should.Throw<BusinessException>(() => Check(open, close, min, max));
        ex.Code.ShouldBe(PortalDomainErrorCodes.NarrowingViolation);
    }

    [Fact]
    public void Close_must_stay_after_open_even_when_both_narrow()
    {
        var ex = Should.Throw<BusinessException>(() => Check(open: 12, close: 12));
        ex.Code.ShouldBe(PortalDomainErrorCodes.InvalidHours);
    }

    [Fact]
    public void An_open_override_is_checked_against_the_inherited_close()
    {
        var ex = Should.Throw<BusinessException>(() => Check(open: 20));
        ex.Code.ShouldBe(PortalDomainErrorCodes.InvalidHours);
    }
}
