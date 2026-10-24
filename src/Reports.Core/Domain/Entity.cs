namespace Reports.Core.Domain;

/// <summary>
/// Базовий клас усіх сутностей предметної області.
/// Корінь дерева спадкування (потрібен для метрик DIT/NOC у лабораторній роботі №7).
/// </summary>
public abstract class Entity
{
    public int Id { get; set; }

    /// <summary>Коротке подання сутності для списків інтерфейсу.</summary>
    public abstract string Display { get; }

    public override string ToString() => Display;

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != 0;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
