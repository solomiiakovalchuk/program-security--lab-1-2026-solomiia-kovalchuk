using System.Text.RegularExpressions;
using Reports.Core.Identity;
using Reports.Core.Identity.Exceptions;
using Reports.Core.Identity.Policies;

namespace Reports.Desktop.Infrastructure;

/// <summary>Український текст для ролей і повідомлень доменних правил (служби повертають англійською).</summary>
public static class UiText
{
    /// <summary>Єдина відповідь на будь-яку відмову входу: нічого не розкриває про причину.</summary>
    public const string LoginFailed = "Невірний логін або пароль, або обліковий запис недоступний.";

    private static readonly (string En, string Uk)[] Phrases =
    {
        ("Password must not be empty.", "Пароль не може бути порожнім."),
        ($"Password must be at most {PasswordPolicy.MaxLength} characters long.",
            $"Пароль має містити не більше {PasswordPolicy.MaxLength} символів."),
        ("Password must not contain control characters.", "Пароль не може містити керівних символів."),
        ("Password must not be equal to the login.", "Пароль не може збігатися з логіном."),
        ($"Password must be at least {PasswordPolicy.MinLengthWhenRestricted} characters long.",
            $"Пароль має містити щонайменше {PasswordPolicy.MinLengthWhenRestricted} символів."),
        ("Password must contain at least one digit.", "Пароль має містити хоча б одну цифру."),
        ("Password must contain at least one punctuation or symbol character.",
            "Пароль має містити хоча б один розділовий знак або символ."),
        ("Current password is incorrect.", "Поточний пароль невірний."),
        ("The new password must differ from the current password.", "Новий пароль має відрізнятися від поточного."),
        ("No password is set for this account. Set the initial password first.",
            "Для цього облікового запису ще не задано пароль."),
        ("The initial password can be set only while no password is set.", "Початковий пароль уже задано."),
        ("Account not found.", "Обліковий запис не знайдено."),
        ("Login may contain only letters, digits, '.', '_' and '-'.",
            "Логін може містити лише літери, цифри та символи «.», «_», «-»."),
    };

    private static readonly (Regex Pattern, string Replacement)[] Patterns =
    {
        (new Regex(@"An account with login '(.+)' already exists\."), "Обліковий запис із логіном «$1» уже існує."),
        (new Regex(@"Login must be (\d+) to (\d+) characters long\."), "Логін має містити від $1 до $2 символів."),
        (new Regex(@"The last active administrator cannot be blocked\."), "Не можна заблокувати останнього активного адміністратора."),
        (new Regex(@"The last active administrator cannot be deleted\."), "Не можна видалити останнього активного адміністратора."),
        (new Regex(@"The last active administrator cannot be demoted\."), "Не можна понизити останнього активного адміністратора."),
        (new Regex(@"The built-in account '(.+)' cannot be (.+)\."), "Вбудований обліковий запис «$1» не можна змінювати таким чином ($2)."),
        (new Regex(@"You cannot have your own account (.+)\."), "Над власним обліковим записом ця дія недоступна."),
    };

    /// <summary>Назва ролі для інтерфейсу.</summary>
    public static string RoleName(Role role) => role switch
    {
        Role.Administrator => "Адміністратор",
        Role.Operator => "Оператор",
        _ => role.ToString()
    };

    /// <summary>Перекладає повідомлення служби на українську; невідомий текст лишається як є.</summary>
    public static string Translate(string message)
    {
        var text = message;
        foreach (var (en, uk) in Phrases)
            text = text.Replace(en, uk, StringComparison.Ordinal);
        foreach (var (pattern, replacement) in Patterns)
            text = pattern.Replace(text, replacement);
        return text;
    }

    /// <summary>Повідомлення про помилку для користувача.</summary>
    public static string Describe(Exception ex) => ex switch
    {
        AccessDeniedException => "Недостатньо прав для цієї дії.",
        DomainRuleViolationException => Translate(ex.Message),
        _ => ex.Message
    };
}
