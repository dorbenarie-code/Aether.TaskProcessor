using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class ManagerShiftCapacityOverrideTests
{
    [Fact]
    public void ApplyToShifts_ShouldOverrideTargetShiftCapacity_AndPreserveMetadata()
    {
        var targetShift = new Shift(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new DateTime(2026, 6, 18, 22, 40, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 19, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true,
            nightShiftCategory: NightShiftCategory.Regular);

        var otherShift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 6, 19, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 19, 14, 20, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresPreferenceToAssign: true);

        var constraintSet = new ManagerConstraintSet(
            shiftCapacityOverrides:
            [
                new ManagerShiftCapacityOverride(
                    targetShift.Id,
                    minResourceCount: 2,
                    maxResourceCount: 2)
            ]);

        var result = new ManagerConstraintApplicator()
            .ApplyToShifts(
                [targetShift, otherShift],
                constraintSet);

        Assert.Equal(2, result.Count);

        var updatedTargetShift = result.Single(shift => shift.Id == targetShift.Id);

        AssertSameIdentityAndMetadata(targetShift, updatedTargetShift);
        Assert.Equal(2, updatedTargetShift.MinResourceCount);
        Assert.Equal(2, updatedTargetShift.MaxResourceCount);

        var unchangedOtherShift = result.Single(shift => shift.Id == otherShift.Id);

        AssertSameIdentityAndMetadata(otherShift, unchangedOtherShift);
        Assert.Equal(otherShift.MinResourceCount, unchangedOtherShift.MinResourceCount);
        Assert.Equal(otherShift.MaxResourceCount, unchangedOtherShift.MaxResourceCount);
    }

    private static void AssertSameIdentityAndMetadata(
        Shift expected,
        Shift actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.StartUtc, actual.StartUtc);
        Assert.Equal(expected.EndUtc, actual.EndUtc);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.NightShiftCategory, actual.NightShiftCategory);
        Assert.Equal(expected.RequiresPreferenceToAssign, actual.RequiresPreferenceToAssign);
        Assert.Equal(expected.RequiresMinimumWhenPreferenceExists, actual.RequiresMinimumWhenPreferenceExists);
    }
}
