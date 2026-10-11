namespace Reports.Identity;

/// <summary>Result of a login attempt, as seen by the UI.</summary>
public enum LoginOutcome
{
    /// <summary>Login succeeded; the main window can open.</summary>
    Success,

    /// <summary>The account has no password yet and must set one.</summary>
    RequiresInitialPassword,

    /// <summary>Login succeeded, but the password must be changed first.</summary>
    RequiresPasswordChange,

    /// <summary>Login was refused; the reason is not disclosed.</summary>
    Failed
}
