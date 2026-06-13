namespace RealEstate.Domain.Common;

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }

    DateTime? ModifiedAtUtc { get; set; }
}
