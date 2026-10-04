using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dixels.Portal.Localization;
using Dixels.Portal.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace Dixels.Portal.Profiles;

/* ABP's own profile service (PUT /api/account/my-profile, POST .../change-password), with what it leaves out:
 * a first name, user name and email are required, a phone number must look like one, and every refusal
 * names the field it is about, so the SPA can show it under that field. ABP still does the work. */
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService), typeof(ProfileAppService), typeof(PortalProfileAppService))]
public class PortalProfileAppService : ProfileAppService
{
    /* The request fields, as the SPA names them (camelCase). */
    public const string NameField = "name";
    public const string UserNameField = "userName";
    public const string EmailField = "email";
    public const string PhoneNumberField = "phoneNumber";
    public const string CurrentPasswordField = "currentPassword";
    public const string NewPasswordField = "newPassword";

    private static readonly Regex PhoneNumber = new(PortalUserConsts.PhoneNumberPattern, RegexOptions.Compiled);

    /* ASP.NET Identity's error codes (its IdentityErrorDescriber method names), by the field each is about.
     * Every other "Password..." code is a rule the new password breaks. */
    private static readonly IReadOnlyDictionary<string, string> FieldByIdentityError = new Dictionary<string, string>
    {
        [nameof(IdentityErrorDescriber.DuplicateUserName)] = UserNameField,
        [nameof(IdentityErrorDescriber.InvalidUserName)] = UserNameField,
        [nameof(IdentityErrorDescriber.DuplicateEmail)] = EmailField,
        [nameof(IdentityErrorDescriber.InvalidEmail)] = EmailField,
        [nameof(IdentityErrorDescriber.PasswordMismatch)] = CurrentPasswordField,
    };

    private const string PasswordRulePrefix = "Password";

    private readonly IStringLocalizer<PortalResource> _portal;

    public PortalProfileAppService(
        IdentityUserManager userManager,
        IOptions<IdentityOptions> identityOptions,
        IStringLocalizer<PortalResource> portal)
        : base(userManager, identityOptions)
    {
        _portal = portal;
    }

    public override async Task<ProfileDto> UpdateAsync(UpdateProfileDto input)
    {
        Require(input.Name, NameField, "Error:FirstNameMissing");
        Require(input.UserName, UserNameField, "Error:UserNameMissing");
        Require(input.Email, EmailField, "Error:EmailMissing");
        CheckPhoneNumber(input.PhoneNumber);

        return await NamingTheFieldAsync(() => base.UpdateAsync(input));
    }

    public override Task ChangePasswordAsync(ChangePasswordInput input)
        => NamingTheFieldAsync(async () =>
        {
            await base.ChangePasswordAsync(input);
            return true;
        });

    private void Require(string? value, string field, string messageKey)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: _portal[messageKey]).ForField(field);
    }

    /* Empty is fine (the phone is optional). */
    private void CheckPhoneNumber(string? phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(phoneNumber) && !PhoneNumber.IsMatch(phoneNumber.Trim()))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidPhone, message: _portal["Error:InvalidPhone"]).ForField(PhoneNumberField);
    }

    /* ABP reports Identity's refusals (a taken user name, a wrong current password, the password rules) with no
     * field; this names the field. Each description is already in the reader's language. */
    private static async Task<T> NamingTheFieldAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (AbpIdentityResultException e) when (FieldOf(e.IdentityResult.Errors) is { } field)
        {
            var message = e.IdentityResult.Errors.Select(error => error.Description).JoinAsString(" ");
            throw new UserFriendlyException(code: PortalDomainErrorCodes.IdentityRejected, message: message, innerException: e).ForField(field);
        }
    }

    /* The one field all the errors are about; null when they are about none, or several. */
    private static string? FieldOf(IEnumerable<IdentityError> errors)
    {
        var fields = errors
            .Select(error => FieldByIdentityError.GetValueOrDefault(error.Code)
                ?? (error.Code.StartsWith(PasswordRulePrefix, StringComparison.Ordinal) ? NewPasswordField : null))
            .Distinct()
            .ToList();
        return fields.Count == 1 ? fields[0] : null;
    }
}
