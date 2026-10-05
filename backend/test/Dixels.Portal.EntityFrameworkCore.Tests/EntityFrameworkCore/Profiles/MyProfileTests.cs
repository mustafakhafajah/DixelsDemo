using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Portal.Profiles;
using Dixels.Portal.Users;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Data;
using Volo.Abp.DynamicProxy;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.SecurityLog;
using Volo.Abp.TenantManagement;
using Volo.Abp.Validation;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Profiles;

/* My Profile shows the signed-in user's own record: ABP's profile plus confirmations, roles, organisation and
 * dates. Users change their details and password through ABP's own profile service, which has no way to change
 * roles or tenant. Each test makes its own users under a unique tag and signs in as them. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class MyProfileTests : PortalEntityFrameworkCoreTestBase
{
    private const string Password = "1q2w3E*";

    private readonly IMyProfileAppService _myProfile;
    private readonly IProfileAppService _profile;
    private readonly IdentityUserManager _users;
    private readonly IdentityRoleManager _roles;
    private readonly IIdentitySecurityLogRepository _securityLogs;
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guids;

    public MyProfileTests()
    {
        _myProfile = GetRequiredService<IMyProfileAppService>();
        _profile = GetRequiredService<IProfileAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _roles = GetRequiredService<IdentityRoleManager>();
        _securityLogs = GetRequiredService<IIdentitySecurityLogRepository>();
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenants = GetRequiredService<ITenantRepository>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guids = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Everything_held_about_the_user_comes_back()
    {
        var tag = NewTag();
        var user = await CreateUserAsync(tag, u =>
        {
            u.Name = "Layla";
            u.Surname = "Haddad";
            u.SetPhoneNumber("+962790000000", confirmed: true);
            u.SetEmailConfirmed(true);
            u.SetProperty(PortalUserConsts.AddressPropertyName, "12 Rainbow Street, Amman");
        }, $"{tag}-zeta", $"{tag}-alpha");

        var profile = await GetAsAsync(user);

        profile.Profile.UserName.ShouldBe(user.UserName);
        profile.Profile.Email.ShouldBe(user.Email);
        profile.Profile.Name.ShouldBe("Layla");
        profile.Profile.Surname.ShouldBe("Haddad");
        profile.Profile.PhoneNumber.ShouldBe("+962790000000");
        profile.Profile.HasPassword.ShouldBeTrue();
        profile.Profile.IsExternal.ShouldBeFalse();
        profile.Profile.GetProperty<string>(PortalUserConsts.AddressPropertyName).ShouldBe("12 Rainbow Street, Amman");
        profile.EmailConfirmed.ShouldBeTrue();
        profile.PhoneNumberConfirmed.ShouldBeTrue();
        profile.TwoFactorEnabled.ShouldBeFalse();
        profile.Roles.ShouldBe(new[] { $"{tag}-alpha", $"{tag}-zeta" });
        profile.TenantName.ShouldBeNull();
        profile.CreationTime.ShouldBe(user.CreationTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_user_with_nothing_optional_gets_empty_values_not_an_error()
    {
        var user = await CreateUserAsync(NewTag());

        var profile = await GetAsAsync(user);

        profile.Profile.Name.ShouldBeNull();
        profile.Profile.Surname.ShouldBeNull();
        profile.Profile.PhoneNumber.ShouldBeNull();
        profile.Profile.GetProperty<string>(PortalUserConsts.AddressPropertyName).ShouldBeNull();
        profile.EmailConfirmed.ShouldBeFalse();
        profile.PhoneNumberConfirmed.ShouldBeFalse();
        profile.Roles.ShouldBeEmpty();
        profile.LastSignInTime.ShouldBeNull();
        profile.LastPasswordChangeTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task Only_the_signed_in_users_own_record_is_returned()
    {
        var tag = NewTag();
        var first = await CreateUserAsync(tag + "a");
        var second = await CreateUserAsync(tag + "b");

        (await GetAsAsync(first)).Profile.UserName.ShouldBe(first.UserName);
        (await GetAsAsync(second)).Profile.UserName.ShouldBe(second.UserName);
    }

    [Fact]
    public async Task A_tenant_user_sees_their_organisation()
    {
        var tenantName = NewTag();
        var tenant = await WithUnitOfWorkAsync(async () => await _tenants.InsertAsync(await _tenantManager.CreateAsync(tenantName)));

        using (_currentTenant.Change(tenant.Id, tenant.Name))
        {
            var user = await CreateUserAsync(NewTag());

            var profile = await GetAsAsync(user);

            profile.TenantName.ShouldBe(tenantName);
            profile.Profile.UserName.ShouldBe(user.UserName);
        }
    }

    [Fact]
    public async Task Last_sign_in_is_the_newest_successful_sign_in_in_the_security_log()
    {
        var user = await CreateUserAsync(NewTag());
        var other = await CreateUserAsync(NewTag());
        var now = DateTime.UtcNow;

        await WithUnitOfWorkAsync(async () =>
        {
            await LogAsync(user, IdentitySecurityLogActionConsts.LoginSucceeded, now.AddDays(-2));
            await LogAsync(user, IdentitySecurityLogActionConsts.LoginSucceeded, now.AddHours(-1));
            /* Neither a failed attempt nor someone else's sign-in counts. */
            await LogAsync(user, IdentitySecurityLogActionConsts.LoginFailed, now.AddMinutes(-5));
            await LogAsync(other, IdentitySecurityLogActionConsts.LoginSucceeded, now.AddMinutes(-1));
        });

        (await GetAsAsync(user)).LastSignInTime.ShouldNotBeNull().ShouldBe(now.AddHours(-1), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Without_a_logged_sign_in_the_users_own_last_sign_in_time_is_used()
    {
        var signedIn = DateTimeOffset.UtcNow.AddDays(-3);
        var user = await CreateUserAsync(NewTag(), u => u.SetLastSignInTime(signedIn));

        (await GetAsAsync(user)).LastSignInTime.ShouldNotBeNull().ShouldBe(signedIn.UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_user_can_change_their_own_details_and_address_but_their_roles_stay()
    {
        var tag = NewTag();
        var user = await CreateUserAsync(tag, u => u.SetEmailConfirmed(true), $"{tag}-crew");

        var input = await CurrentProfileInputAsync(user);
        input.UserName = tag + ".renamed";
        input.Name = "Ammar";
        input.Surname = "Safwan";
        input.PhoneNumber = "+962790000001";
        input.SetProperty(PortalUserConsts.AddressPropertyName, "7 New Street, Irbid");
        await AsAsync(user, () => _profile.UpdateAsync(input));

        var profile = await GetAsAsync(user);
        profile.Profile.UserName.ShouldBe(tag + ".renamed");
        profile.Profile.Name.ShouldBe("Ammar");
        profile.Profile.Surname.ShouldBe("Safwan");
        profile.Profile.PhoneNumber.ShouldBe("+962790000001");
        profile.Profile.GetProperty<string>(PortalUserConsts.AddressPropertyName).ShouldBe("7 New Street, Irbid");
        /* A new phone number has not been confirmed yet. */
        profile.PhoneNumberConfirmed.ShouldBeFalse();
        profile.Roles.ShouldBe(new[] { $"{tag}-crew" });
        profile.TenantName.ShouldBeNull();
    }

    [Fact]
    public async Task A_user_cannot_change_their_own_email()
    {
        var tag = NewTag();
        var user = await CreateUserAsync(tag, u => { u.Name = "Ammar"; u.SetEmailConfirmed(true); });

        var input = await CurrentProfileInputAsync(user);
        input.Email = $"{tag}.new@example.com";
        await AsAsync(user, () => _profile.UpdateAsync(input));

        var profile = await GetAsAsync(user);
        profile.Profile.Email.ShouldBe(user.Email);
        profile.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task An_address_longer_than_allowed_is_refused()
    {
        var user = await CreateUserAsync(NewTag());

        var input = await CurrentProfileInputAsync(user);
        /* Not checked here, as a request from the browser would not be; the server must refuse it. */
        input.SetProperty(PortalUserConsts.AddressPropertyName, new string('x', PortalUserConsts.MaxAddressLength + 1), validate: false);

        await Should.ThrowAsync<AbpValidationException>(() => AsAsync(user, () => _profile.UpdateAsync(input)));
    }

    [Fact]
    public void The_portal_profile_service_stands_in_for_abps()
        => ProxyHelper.UnProxy(_profile).ShouldBeAssignableTo<PortalProfileAppService>();

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_first_name_is_required(string? name)
    {
        var user = await CreateUserAsync(NewTag(), u => u.Name = "Ammar");
        var input = await CurrentProfileInputAsync(user);
        input.Name = name;

        await ShouldBeRefusedForAsync(PortalProfileAppService.NameField, () => AsAsync(user, () => _profile.UpdateAsync(input)));
    }

    [Fact]
    public async Task A_user_name_and_an_email_are_required()
    {
        var user = await CreateUserAsync(NewTag(), u => u.Name = "Ammar");

        var noUserName = await CurrentProfileInputAsync(user);
        noUserName.UserName = "";
        await ShouldBeRefusedForAsync(PortalProfileAppService.UserNameField, () => AsAsync(user, () => _profile.UpdateAsync(noUserName)));

        var noEmail = await CurrentProfileInputAsync(user);
        noEmail.Email = " ";
        await ShouldBeRefusedForAsync(PortalProfileAppService.EmailField, () => AsAsync(user, () => _profile.UpdateAsync(noEmail)));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12345")]
    [InlineData("+962 79 000")]
    [InlineData("0796-000-000")]
    public async Task A_phone_number_that_is_not_one_is_refused(string phoneNumber)
    {
        var user = await CreateUserAsync(NewTag(), u => u.Name = "Ammar");
        var input = await CurrentProfileInputAsync(user);
        input.PhoneNumber = phoneNumber;

        await ShouldBeRefusedForAsync(PortalProfileAppService.PhoneNumberField, () => AsAsync(user, () => _profile.UpdateAsync(input)));
    }

    [Theory]
    [InlineData("+962790000000")]
    [InlineData("00962778448455")]
    [InlineData("")]
    public async Task A_phone_number_of_digits_or_none_is_accepted(string phoneNumber)
    {
        var user = await CreateUserAsync(NewTag(), u => u.Name = "Ammar");
        var input = await CurrentProfileInputAsync(user);
        input.PhoneNumber = phoneNumber;

        await AsAsync(user, () => _profile.UpdateAsync(input));
    }

    [Fact]
    public async Task A_user_name_someone_else_has_is_refused_under_the_user_name()
    {
        var tag = NewTag();
        var taken = await CreateUserAsync(tag + "a");
        var user = await CreateUserAsync(tag + "b", u => u.Name = "Ammar");
        var input = await CurrentProfileInputAsync(user);
        input.UserName = taken.UserName;

        await ShouldBeRefusedForAsync(PortalProfileAppService.UserNameField, () => AsAsync(user, () => _profile.UpdateAsync(input)));
    }

    [Fact]
    public async Task A_wrong_current_password_is_refused_under_the_current_password()
    {
        var user = await CreateUserAsync(NewTag());

        await ShouldBeRefusedForAsync(PortalProfileAppService.CurrentPasswordField, () =>
            AsAsync(user, () => _profile.ChangePasswordAsync(new ChangePasswordInput { CurrentPassword = "Wr0ng-pass!", NewPassword = "N3w-pass!" })));
    }

    [Fact]
    public async Task Every_password_rule_the_new_password_breaks_is_listed_under_the_new_password()
    {
        var user = await CreateUserAsync(NewTag());

        var refusal = await ShouldBeRefusedForAsync(PortalProfileAppService.NewPasswordField, () =>
            AsAsync(user, () => _profile.ChangePasswordAsync(new ChangePasswordInput { CurrentPassword = Password, NewPassword = "abcdefg" })));

        /* No digit, no capital, no symbol: three rules, one message under one field. */
        refusal.Message.ShouldContain("digit");
        refusal.Message.ShouldContain("uppercase");
        refusal.Message.ShouldContain("non alphanumeric");
    }

    [Fact]
    public async Task A_user_can_change_their_password_only_with_the_current_one()
    {
        const string newPassword = "N3w-pass!";
        var user = await CreateUserAsync(NewTag());

        await Should.ThrowAsync<UserFriendlyException>(() =>
            AsAsync(user, () => _profile.ChangePasswordAsync(new ChangePasswordInput { CurrentPassword = "Wr0ng-pass!", NewPassword = newPassword })));

        await AsAsync(user, () => _profile.ChangePasswordAsync(new ChangePasswordInput { CurrentPassword = Password, NewPassword = newPassword }));

        await WithUnitOfWorkAsync(async () =>
        {
            var saved = await _users.GetByIdAsync(user.Id);
            (await _users.CheckPasswordAsync(saved, newPassword)).ShouldBeTrue();
            (await _users.CheckPasswordAsync(saved, Password)).ShouldBeFalse();
        });
    }

    [Fact]
    public void Any_signed_in_user_may_read_their_profile_without_a_permission()
    {
        var authorize = typeof(MyProfileAppService).GetCustomAttribute<AuthorizeAttribute>();

        authorize.ShouldNotBeNull();
        authorize.Policy.ShouldBeNull();
    }

    private static string NewTag() => "me" + Guid.NewGuid().ToString("N")[..8];

    private Task<MyProfileDto> GetAsAsync(IdentityUser user) => AsAsync(user, _myProfile.GetAsync);

    /* Runs a call the way the signed-in user would make it: their id (and tenant) in the claims. */
    private async Task<T> AsAsync<T>(IdentityUser user, Func<Task<T>> call)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, user.Id.ToString()),
            new(AbpClaimTypes.UserName, user.UserName),
        };
        if (user.TenantId.HasValue)
            claims.Add(new Claim(AbpClaimTypes.TenantId, user.TenantId.Value.ToString()));

        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity(claims))))
        {
            return await call();
        }
    }

    private Task AsAsync(IdentityUser user, Func<Task> call) => AsAsync(user, async () => { await call(); return true; });

    /* Refused with a message the SPA shows under that field. */
    private static async Task<UserFriendlyException> ShouldBeRefusedForAsync(string field, Func<Task> call)
    {
        var refusal = await Should.ThrowAsync<UserFriendlyException>(call);
        refusal.Data[ErrorFieldExtensions.FieldKey].ShouldBe(field);
        return refusal;
    }

    /* ABP's own update input, filled from what the user sees now, then changed by the test. */
    private async Task<UpdateProfileDto> CurrentProfileInputAsync(IdentityUser user)
    {
        var current = (await GetAsAsync(user)).Profile;
        var input = new UpdateProfileDto
        {
            UserName = current.UserName,
            Email = current.Email,
            Name = current.Name,
            Surname = current.Surname,
            PhoneNumber = current.PhoneNumber,
            ConcurrencyStamp = current.ConcurrencyStamp,
        };
        input.SetProperty(PortalUserConsts.AddressPropertyName, current.GetProperty<string>(PortalUserConsts.AddressPropertyName));
        return input;
    }

    /* A user named after the tag, in the current tenant, optionally adjusted and given the named roles. */
    private Task<IdentityUser> CreateUserAsync(string tag, Action<IdentityUser>? adjust = null, params string[] roles) => WithUnitOfWorkAsync(async () =>
    {
        var user = new IdentityUser(_guids.Create(), tag, $"{tag}@dixels.io", _currentTenant.Id);
        adjust?.Invoke(user);
        (await _users.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();

        foreach (var role in roles)
        {
            (await _roles.CreateAsync(new IdentityRole(_guids.Create(), role, _currentTenant.Id))).Succeeded.ShouldBeTrue();
            (await _users.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }

        return user;
    });

    private Task LogAsync(IdentityUser user, string action, DateTime at) => _securityLogs.InsertAsync(
        new IdentitySecurityLog(_guids, new SecurityLogInfo
        {
            Identity = IdentitySecurityLogIdentityConsts.Identity,
            Action = action,
            UserId = user.Id,
            UserName = user.UserName,
            TenantId = user.TenantId,
            CreationTime = at,
        }));
}
