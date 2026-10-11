namespace Reports.Core.Entities;

/// <summary>Base class for domain entities, identified by their Id.</summary>
public abstract class Entity
{
    /// <summary>Creates an entity with a new identifier.</summary>
    protected Entity()
        : this(Guid.NewGuid())
    {
    }

    /// <summary>Creates an entity with the given identifier.</summary>
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Entity identifier must not be empty.", nameof(id));
        }

        Id = id;
    }

    /// <summary>Unique identifier of the entity.</summary>
    public Guid Id { get; }
}
