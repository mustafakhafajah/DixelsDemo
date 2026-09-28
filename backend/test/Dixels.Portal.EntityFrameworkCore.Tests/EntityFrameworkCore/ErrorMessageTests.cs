using System;
using Dixels.Portal.Bookings;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore;

/* A rule the user breaks must reach the browser as our own sentence, not ABP's generic
 * "An internal error occurred during your request!", which is what a plain BusinessException becomes. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class ErrorMessageTests : PortalEntityFrameworkCoreTestBase
{
    [Fact]
    public void A_broken_rule_sends_its_message_and_code_to_the_client()
    {
        var converter = GetRequiredService<IExceptionToErrorInfoConverter>();
        var rule = new UserFriendlyException(code: PortalDomainErrorCodes.BookingSelfOverlap,
            message: "You already have Room A booked from 09:00 to 10:00.");

        var error = converter.Convert(rule, _ => { });

        error.Message.ShouldBe("You already have Room A booked from 09:00 to 10:00.");
        error.Code.ShouldBe(PortalDomainErrorCodes.BookingSelfOverlap);
    }
}
