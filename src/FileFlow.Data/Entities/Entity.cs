namespace FileFlow.Data.Entities;

public abstract class Entity
{
    public virtual Guid Id { get; protected set; }

    protected Entity() { }
}