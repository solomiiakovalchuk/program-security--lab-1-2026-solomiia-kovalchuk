using Reports.Core.Identity.Policies;

namespace Reports.Identity.Tests;

/// <summary>
/// Tests for <see cref="PasswordPolicy"/>.
/// </summary>
public sealed class PasswordPolicyTests
{
    [Fact]
    public void PasswordPolicy_Rejects_EmptyOrTooLong_OrEqualToLogin()
    {
        Assert.NotEmpty(PasswordPolicy.Validate(string.Empty, "operator1", restrictionsEnabled: false));
        Assert.NotEmpty(PasswordPolicy.Validate(new string('a', PasswordPolicy.MaxLength + 1), "operator1", restrictionsEnabled: false));
        Assert.NotEmpty(PasswordPolicy.Validate("Operator1", "operator1", restrictionsEnabled: false));

        Assert.Empty(PasswordPolicy.Validate(new string('a', PasswordPolicy.MaxLength), "operator1", restrictionsEnabled: false));
    }

    [Fact]
    public void PasswordPolicy_WithRestrictions_RequiresDigitAndPunctuation()
    {
        Assert.NotEmpty(PasswordPolicy.Validate("Ab1!", "operator1", restrictionsEnabled: true));      // too short
        Assert.NotEmpty(PasswordPolicy.Validate("abcdefgh!", "operator1", restrictionsEnabled: true)); // no digit
        Assert.NotEmpty(PasswordPolicy.Validate("abcdefg1", "operator1", restrictionsEnabled: true));  // no punctuation or symbol

        Assert.Empty(PasswordPolicy.Validate("abcdef1!", "operator1", restrictionsEnabled: true));
        Assert.Empty(PasswordPolicy.Validate("abcdefg", "operator1", restrictionsEnabled: false));
    }
}
