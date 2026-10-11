namespace Reports.Core.Identity.Policies;

/// <summary>Password rules applied when a password is set or changed.</summary>
public static class PasswordPolicy
{
    /// <summary>Maximum password length.</summary>
    public const int MaxLength = 128;

    /// <summary>Minimum password length when restrictions are enabled.</summary>
    public const int MinLengthWhenRestricted = 8;

    /// <summary>Returns the policy violations of a password; an empty list means it is valid.</summary>
    public static IReadOnlyList<string> Validate(string password, string login, bool restrictionsEnabled)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(login);

        var violations = new List<string>();

        if (password.Length == 0)
        {
            violations.Add("Password must not be empty.");
        }

        if (password.Length > MaxLength)
        {
            violations.Add($"Password must be at most {MaxLength} characters long.");
        }

        if (password.Any(char.IsControl))
        {
            violations.Add("Password must not contain control characters.");
        }

        if (password.Length > 0 && string.Equals(password, login, StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("Password must not be equal to the login.");
        }

        if (restrictionsEnabled)
        {
            if (password.Length < MinLengthWhenRestricted)
            {
                violations.Add($"Password must be at least {MinLengthWhenRestricted} characters long.");
            }

            if (!password.Any(char.IsDigit))
            {
                violations.Add("Password must contain at least one digit.");
            }

            if (!password.Any(character => char.IsPunctuation(character) || char.IsSymbol(character)))
            {
                violations.Add("Password must contain at least one punctuation or symbol character.");
            }
        }

        return violations;
    }
}
