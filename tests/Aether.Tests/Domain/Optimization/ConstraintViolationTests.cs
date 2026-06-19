using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ConstraintViolationTests
{
    [Fact]
    public void Constraint_violation_requires_valid_type()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConstraintViolation(
                (ConstraintViolationType)999,
                ConstraintViolationSeverity.Hard,
                "Invalid violation type."));

        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void Constraint_violation_requires_valid_severity()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                (ConstraintViolationSeverity)999,
                "Invalid severity."));

        Assert.Equal("severity", exception.ParamName);
    }

    [Fact]
    public void Constraint_violation_requires_message()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                " "));

        Assert.Equal("message", exception.ParamName);
    }

    [Fact]
    public void Constraint_violation_trims_message()
    {
        var violation = new ConstraintViolation(
            ConstraintViolationType.ResourceUnavailable,
            ConstraintViolationSeverity.Hard,
            "  Resource is unavailable.  ");

        Assert.Equal("Resource is unavailable.", violation.Message);
    }

    [Fact]
    public void Constraint_violation_rejects_empty_resource_id_when_provided()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ConstraintViolation(
                ConstraintViolationType.ResourceUnavailable,
                ConstraintViolationSeverity.Hard,
                "Resource is unavailable.",
                resourceId: Guid.Empty));

        Assert.Equal("resourceId", exception.ParamName);
    }

    [Fact]
    public void Constraint_violation_rejects_empty_shift_id_when_provided()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ConstraintViolation(
                ConstraintViolationType.ShiftUnderstaffed,
                ConstraintViolationSeverity.Hard,
                "Shift is understaffed.",
                shiftId: Guid.Empty));

        Assert.Equal("shiftId", exception.ParamName);
    }

    [Fact]
    public void Constraint_violation_can_reference_resource_and_shift()
    {
        var resourceId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();

        var violation = new ConstraintViolation(
            ConstraintViolationType.ResourceUnavailable,
            ConstraintViolationSeverity.Hard,
            "Resource is unavailable for this shift.",
            resourceId,
            shiftId);

        Assert.Equal(ConstraintViolationType.ResourceUnavailable, violation.Type);
        Assert.Equal(ConstraintViolationSeverity.Hard, violation.Severity);
        Assert.Equal("Resource is unavailable for this shift.", violation.Message);
        Assert.Equal(resourceId, violation.ResourceId);
        Assert.Equal(shiftId, violation.ShiftId);
    }
    [Fact]
    public void Constraint_violation_allows_missing_magnitude_by_default()
    {
        var violation = new ConstraintViolation(
            ConstraintViolationType.ResourceUnavailable,
            ConstraintViolationSeverity.Hard,
            "Resource is unavailable.");

        Assert.Null(violation.Magnitude);
    }

    [Fact]
    public void Constraint_violation_stores_magnitude()
    {
        var violation = new ConstraintViolation(
            ConstraintViolationType.IgnoredAvoidPreference,
            ConstraintViolationSeverity.Soft,
            "Soft scheduling issue.",
            magnitude: 12.5);

        Assert.Equal(12.5, violation.Magnitude);
    }

    [Fact]
    public void Constraint_violation_rejects_negative_magnitude()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConstraintViolation(
                ConstraintViolationType.IgnoredAvoidPreference,
                ConstraintViolationSeverity.Soft,
                "Soft scheduling issue.",
                magnitude: -1));

        Assert.Equal("magnitude", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constraint_violation_rejects_non_finite_magnitude(double magnitude)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConstraintViolation(
                ConstraintViolationType.IgnoredAvoidPreference,
                ConstraintViolationSeverity.Soft,
                "Soft scheduling issue.",
                magnitude: magnitude));

        Assert.Equal("magnitude", exception.ParamName);
    }

}
