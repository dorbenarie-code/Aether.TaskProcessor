namespace Aether.Domain.Optimization;

public sealed class ConstraintViolation
{
    public ConstraintViolationType Type { get; }
    public ConstraintViolationSeverity Severity { get; }
    public string Message { get; }
    public Guid? ResourceId { get; }
    public Guid? ShiftId { get; }
    public double? Magnitude { get; }

    public ConstraintViolation(
        ConstraintViolationType type,
        ConstraintViolationSeverity severity,
        string message,
        Guid? resourceId = null,
        Guid? shiftId = null,
        double? magnitude = null)
    {
        if (!Enum.IsDefined(typeof(ConstraintViolationType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Constraint violation type is not supported.");
        }

        if (!Enum.IsDefined(typeof(ConstraintViolationSeverity), severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), "Constraint violation severity is not supported.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Constraint violation message is required.", nameof(message));
        }

        if (resourceId == Guid.Empty)
        {
            throw new ArgumentException("Resource id cannot be empty.", nameof(resourceId));
        }

        if (shiftId == Guid.Empty)
        {
            throw new ArgumentException("Shift id cannot be empty.", nameof(shiftId));
        }

        if (magnitude.HasValue && !double.IsFinite(magnitude.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(magnitude),
                "Constraint violation magnitude must be a finite number.");
        }

        if (magnitude.HasValue && magnitude.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(magnitude),
                "Constraint violation magnitude cannot be negative.");
        }

        Type = type;
        Severity = severity;
        Message = message.Trim();
        ResourceId = resourceId;
        ShiftId = shiftId;
        Magnitude = magnitude;
    }
}
