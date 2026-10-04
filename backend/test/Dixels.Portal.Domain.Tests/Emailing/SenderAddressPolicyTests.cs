using Shouldly;
using Xunit;

namespace Dixels.Portal.Emailing;

/* SenderAddressPolicy is pure, so these need no database or ABP base class. */
public class SenderAddressPolicyTests
{
    private const string Mailbox = "noreply@dixels.io";

    [Theory]
    [InlineData("noreply@dixels.io")]
    [InlineData("NoReply@Dixels.io")]
    public void The_service_mailbox_may_send(string from) => SenderAddressPolicy.IsAllowed(from, Mailbox, null).ShouldBeTrue();

    [Fact]
    public void An_allowed_alias_may_send() =>
        SenderAddressPolicy.IsAllowed("bookings@dixels.io", Mailbox, "facilities@dixels.io, bookings@dixels.io").ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("facilities@dixels.io")]
    public void Any_other_address_may_not(string? allowed) =>
        SenderAddressPolicy.IsAllowed("ceo@dixels.io", Mailbox, allowed).ShouldBeFalse();
}
