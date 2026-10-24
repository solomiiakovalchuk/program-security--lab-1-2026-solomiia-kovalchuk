namespace Reports.Core.Domain;

/// <summary>
/// Запис довідника «Люди»: підписант рапорту та/або оператор.
/// Email — унікальний ключ рядка; для операторів має збігатися з акаунтом Google,
/// щоб працював фільтр за оператором.
/// </summary>
public sealed class Person : Entity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;

    public override string Display =>
        string.IsNullOrWhiteSpace(FullName) ? Email : $"{FullName} ({Rank})";

    /// <summary>Чи достатньо даних, щоб сформувати підпис під рапортом.</summary>
    public bool CanSign =>
        !string.IsNullOrWhiteSpace(FullName)
        && !string.IsNullOrWhiteSpace(Rank)
        && !string.IsNullOrWhiteSpace(Position);
}
