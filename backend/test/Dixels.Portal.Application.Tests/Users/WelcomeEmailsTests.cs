using Shouldly;
using Xunit;

namespace Dixels.Portal.Users;

public class WelcomeEmailsTests
{
    [Fact]
    public void An_account_made_by_an_admin_gets_a_set_password_link()
    {
        var email = WelcomeEmails.Welcome("Sara", "sara", "https://server.example/Account/ResetPassword?userId=1&resetToken=abc", "https://portal.example");

        email.Subject.ShouldBe("Welcome to Dixels Portal: set your password");
        email.Html.ShouldContain("Set my password");
        email.Html.ShouldContain("https://server.example/Account/ResetPassword?userId=1&amp;resetToken=abc");
        email.Html.ShouldContain("<strong>sara</strong>");
        email.Html.ShouldContain("https://portal.example");
    }

    [Fact]
    public void A_self_sign_up_gets_a_plain_welcome()
    {
        var email = WelcomeEmails.Welcome("Sara", "sara", null, "https://portal.example");

        email.Subject.ShouldBe("Welcome to Dixels Portal");
        email.Html.ShouldNotContain("password");
        email.Html.ShouldContain("Open the portal");
    }
}
