namespace Reports.Core.Domain;

/// <summary>
/// Базовий клас елемента довідника.
/// <para><see cref="ShortName"/> — те, що бачить оператор у списку форми.</para>
/// <para><see cref="FullName"/> — те, що підставляється у текст рапорту.</para>
/// </summary>
public abstract class ReferenceItem : Entity
{
    public string ShortName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;

    /// <summary>Назва блоку довідника на аркуші «Довідники» Google-таблиці.</summary>
    public abstract string DirectoryTitle { get; }

    public override string Display => ShortName;

    /// <summary>
    /// Назва, що підставляється у документ. Порожня повна назва — не привід
    /// зривати формування рапорту, тому відкочуємось на коротку.
    /// </summary>
    public virtual string ForDocument() =>
        string.IsNullOrWhiteSpace(FullName) ? ShortName : FullName;
}

/// <summary>Тип безпілотного літального апарата.</summary>
public sealed class UavType : ReferenceItem
{
    public override string DirectoryTitle => "БпЛА";
}

/// <summary>
/// Тип цілі. Повна назва зберігається у <b>родовому відмінку</b>,
/// бо підставляється у фразу «по знищенню …».
/// </summary>
public sealed class TargetType : ReferenceItem
{
    public override string DirectoryTitle => "ЦІЛІ";
}

/// <summary>Тип боєприпасу.</summary>
public sealed class MunitionType : ReferenceItem
{
    public override string DirectoryTitle => "БОЄПРИПАСИ";
}

/// <summary>
/// Населений пункт. Тут <see cref="ReferenceItem.FullName"/> не використовується —
/// друга колонка довідника містить область, тому перевизначаємо <see cref="ForDocument"/>.
/// </summary>
public sealed class Settlement : ReferenceItem
{
    public override string DirectoryTitle => "МІСЦЯ";

    /// <summary>Область, яка підставляється у тег {{ОБЛАСТЬ}}.</summary>
    public string Region { get; set; } = string.Empty;

    public override string ForDocument() => ShortName;
}
