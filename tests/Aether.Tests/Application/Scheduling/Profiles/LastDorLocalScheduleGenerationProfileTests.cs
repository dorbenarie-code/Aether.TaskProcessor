using Aether.Application.Scheduling.Profiles;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling.Profiles;

public sealed class LastDorLocalScheduleGenerationProfileTests
{
    [Fact]
    public void CreateInputs_ShouldReturnCurrentLastDorCleanScheduleDefaults()
    {
        var profile = new LastDorLocalScheduleGenerationProfile();

        var period = profile.CreateSchedulePeriod();
        var resources = profile.CreateResources();
        var shifts = profile.CreateShifts();

        Assert.Equal(
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            period.StartUtc);

        Assert.Equal(
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc),
            period.EndUtc);

        Assert.Equal(736.0, profile.TotalEffectiveTargetHours);
        Assert.Equal(5.0, profile.MaximumAssignedHoursDeviationFromAverageHours);
        Assert.Equal(20260603, profile.Seed);

        Assert.Equal(19, resources.Count);
        Assert.Contains(resources, resource => resource.Name == "Worker14");
        Assert.Contains(resources, resource => resource.Name == "Worker16");
        Assert.Contains(resources, resource => resource.Name == "Worker18");

        Assert.Equal(42, shifts.Count);

        var sundayMorning = shifts.Single(shift =>
            DateOnly.FromDateTime(shift.StartUtc) == new DateOnly(2026, 6, 14) &&
            shift.Kind == ShiftKind.Morning);

        Assert.Equal(4, sundayMorning.MinResourceCount);
        Assert.Equal(6, sundayMorning.MaxResourceCount);
        Assert.False(sundayMorning.RequiresPreferenceToAssign);

        var fridayMorning = shifts.Single(shift =>
            DateOnly.FromDateTime(shift.StartUtc) == new DateOnly(2026, 6, 19) &&
            shift.Kind == ShiftKind.Morning);

        Assert.Equal(0, fridayMorning.MinResourceCount);
        Assert.Equal(2, fridayMorning.MaxResourceCount);
        Assert.True(fridayMorning.RequiresPreferenceToAssign);

        var saturdayNight = shifts.Single(shift =>
            DateOnly.FromDateTime(shift.StartUtc) == new DateOnly(2026, 6, 20) &&
            shift.Kind == ShiftKind.Night);

        Assert.Equal(3, saturdayNight.MinResourceCount);
        Assert.Equal(3, saturdayNight.MaxResourceCount);
        Assert.False(saturdayNight.RequiresPreferenceToAssign);
        Assert.Equal(NightShiftCategory.MotzeiShabbatNight, saturdayNight.NightShiftCategory);
    }
}
