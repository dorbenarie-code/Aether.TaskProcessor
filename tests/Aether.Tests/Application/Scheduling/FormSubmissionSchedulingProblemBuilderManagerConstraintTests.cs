using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.SubmissionForms;
using Aether.Domain.Optimization;

namespace Aether.Tests.Application.Scheduling;

public sealed class FormSubmissionSchedulingProblemBuilderManagerConstraintTests
{
    [Fact]
    public void Build_ShouldApplyManagerForbidAssignment_AfterMandatoryPolicyAndBeforeWorkloadDemands()
    {
        var dana = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var yossi = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Yossi");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            date,
            ShiftKind.Afternoon);

        var period = new SchedulePeriod(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var importResult = new AvailabilityMatrixWorkerSubmissionImporter()
            .Import(new AvailabilityMatrixWorkerSubmissionImportRequest(
                [
                    ["שם המאבטח", "שני - 15/06"],
                    ["Dana", "בוקר"],
                    ["Yossi", "צהריים"]
                ],
                period,
                [dana, yossi]));

        Assert.Empty(importResult.FatalErrors);
        Assert.Empty(importResult.Warnings);

        var managerConstraintSet = new ManagerConstraintSet([
            new ManagerForbiddenAssignment(dana.Id, morningShift.Id)
        ]);

        var buildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(new FormSubmissionSchedulingProblemBuildRequest(
                period,
                [dana, yossi],
                [morningShift, afternoonShift],
                importResult.WorkerSubmissions,
                TotalEffectiveTargetHours: 8,
                ApplyMandatoryShiftAvailabilityPolicy: true,
                ManagerConstraintSet: managerConstraintSet));

        Assert.DoesNotContain(
            buildResult.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == dana.Id &&
                window.Covers(morningShift));

        Assert.DoesNotContain(
            buildResult.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == dana.Id &&
                preference.StartUtc < morningShift.EndUtc &&
                morningShift.StartUtc < preference.EndUtc);

        Assert.Contains(
            buildResult.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == yossi.Id &&
                preference.Type == ResourcePreferenceType.Prefer &&
                preference.StartUtc == afternoonShift.StartUtc &&
                preference.EndUtc == afternoonShift.EndUtc);

        var danaDemand = buildResult.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == dana.Id);

        var yossiDemand = buildResult.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == yossi.Id);

        Assert.Equal(0, danaDemand.RequestedPreferredHours);
        Assert.Equal(8, yossiDemand.RequestedPreferredHours);
    }

    [Fact]
    public void Build_ShouldApplyManagerShiftCapacityOverride_ToSchedulingProblemShifts()
    {
        var resource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var targetShift = new Shift(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new DateTime(2026, 6, 18, 22, 40, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 19, 6, 30, 0, DateTimeKind.Utc),
            ShiftKind.Night,
            minResourceCount: 0,
            maxResourceCount: 1,
            requiresPreferenceToAssign: true,
            requiresMinimumWhenPreferenceExists: true,
            nightShiftCategory: NightShiftCategory.Regular);

        var otherShift = new Shift(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            new DateTime(2026, 6, 19, 6, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 19, 14, 20, 0, DateTimeKind.Utc),
            ShiftKind.Morning,
            minResourceCount: 0,
            maxResourceCount: 2,
            requiresPreferenceToAssign: true);

        var period = new SchedulePeriod(
            new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));

        var managerConstraintSet = new ManagerConstraintSet(
            shiftCapacityOverrides:
            [
                new ManagerShiftCapacityOverride(
                    targetShift.Id,
                    minResourceCount: 2,
                    maxResourceCount: 2)
            ]);

        var buildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(new FormSubmissionSchedulingProblemBuildRequest(
                period,
                [resource],
                [targetShift, otherShift],
                WorkerSubmissions: [],
                ManagerConstraintSet: managerConstraintSet));

        var updatedTargetShift = buildResult.Problem.Shifts.Single(
            shift => shift.Id == targetShift.Id);

        Assert.Equal(targetShift.Id, updatedTargetShift.Id);
        Assert.Equal(targetShift.StartUtc, updatedTargetShift.StartUtc);
        Assert.Equal(targetShift.EndUtc, updatedTargetShift.EndUtc);
        Assert.Equal(targetShift.Kind, updatedTargetShift.Kind);
        Assert.Equal(targetShift.NightShiftCategory, updatedTargetShift.NightShiftCategory);
        Assert.Equal(targetShift.RequiresPreferenceToAssign, updatedTargetShift.RequiresPreferenceToAssign);
        Assert.Equal(targetShift.RequiresMinimumWhenPreferenceExists, updatedTargetShift.RequiresMinimumWhenPreferenceExists);
        Assert.Equal(2, updatedTargetShift.MinResourceCount);
        Assert.Equal(2, updatedTargetShift.MaxResourceCount);

        var unchangedOtherShift = buildResult.Problem.Shifts.Single(
            shift => shift.Id == otherShift.Id);

        Assert.Equal(otherShift.MinResourceCount, unchangedOtherShift.MinResourceCount);
        Assert.Equal(otherShift.MaxResourceCount, unchangedOtherShift.MaxResourceCount);
        Assert.Equal(otherShift.StartUtc, unchangedOtherShift.StartUtc);
        Assert.Equal(otherShift.EndUtc, unchangedOtherShift.EndUtc);
        Assert.Equal(otherShift.Kind, unchangedOtherShift.Kind);
        Assert.Equal(otherShift.RequiresPreferenceToAssign, unchangedOtherShift.RequiresPreferenceToAssign);
        Assert.Equal(otherShift.RequiresMinimumWhenPreferenceExists, unchangedOtherShift.RequiresMinimumWhenPreferenceExists);
        Assert.Equal(otherShift.NightShiftCategory, unchangedOtherShift.NightShiftCategory);
    }

    [Fact]
    public void Build_ShouldMakeEvaluatorTreatManagerShiftCapacityOverrideAsHardCapacity()
    {
        var firstResource = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var secondResource = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Yossi");

        var thirdResource = CreateResource(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            "Noa");

        var date = new DateOnly(2026, 6, 15);

        var targetShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            date,
            ShiftKind.Night);

        var period = new SchedulePeriod(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            date.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var managerConstraintSet = new ManagerConstraintSet(
            shiftCapacityOverrides:
            [
                new ManagerShiftCapacityOverride(
                    targetShift.Id,
                    minResourceCount: 2,
                    maxResourceCount: 2)
            ]);

        var buildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(new FormSubmissionSchedulingProblemBuildRequest(
                period,
                [firstResource, secondResource, thirdResource],
                [targetShift],
                [
                    CreateSubmission(firstResource, date, ShiftKind.Night),
                    CreateSubmission(secondResource, date, ShiftKind.Night),
                    CreateSubmission(thirdResource, date, ShiftKind.Night)
                ],
                ManagerConstraintSet: managerConstraintSet));

        var updatedShift = buildResult.Problem.Shifts.Single(
            shift => shift.Id == targetShift.Id);

        Assert.Equal(2, updatedShift.MinResourceCount);
        Assert.Equal(2, updatedShift.MaxResourceCount);

        var evaluator = new ScheduleEvaluator();

        var understaffedCandidate = new ScheduleCandidate([
            new Assignment(firstResource.Id, targetShift.Id)
        ]);

        var understaffedEvaluation = evaluator.Evaluate(
            buildResult.Problem,
            understaffedCandidate);

        var understaffedViolation = Assert.Single(
            understaffedEvaluation.Violations.Where(
                violation => violation.Type == ConstraintViolationType.ShiftUnderstaffed));

        Assert.False(understaffedEvaluation.IsFeasible);
        Assert.Equal(ConstraintViolationSeverity.Hard, understaffedViolation.Severity);
        Assert.Equal(targetShift.Id, understaffedViolation.ShiftId);

        var overstaffedCandidate = new ScheduleCandidate([
            new Assignment(firstResource.Id, targetShift.Id),
            new Assignment(secondResource.Id, targetShift.Id),
            new Assignment(thirdResource.Id, targetShift.Id)
        ]);

        var overstaffedEvaluation = evaluator.Evaluate(
            buildResult.Problem,
            overstaffedCandidate);

        var overstaffedViolation = Assert.Single(
            overstaffedEvaluation.Violations.Where(
                violation => violation.Type == ConstraintViolationType.ShiftOverstaffed));

        Assert.False(overstaffedEvaluation.IsFeasible);
        Assert.Equal(ConstraintViolationSeverity.Hard, overstaffedViolation.Severity);
        Assert.Equal(targetShift.Id, overstaffedViolation.ShiftId);
    }

    [Fact]
    public void Build_ShouldRemoveAvoidedPreferFromWorkloadDemands_WhenManagerAvoidsAssignment()
    {
        var dana = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var yossi = CreateResource(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "Yossi");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            date,
            ShiftKind.Morning);

        var afternoonShift = CreateShift(
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            date,
            ShiftKind.Afternoon);

        var period = new SchedulePeriod(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var managerConstraintSet = new ManagerConstraintSet(
            avoidAssignments:
            [
                new ManagerAvoidAssignment(
                    dana.Id,
                    morningShift.Id)
            ]);

        var buildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(new FormSubmissionSchedulingProblemBuildRequest(
                period,
                [dana, yossi],
                [morningShift, afternoonShift],
                [
                    CreateSubmission(dana, date, ShiftKind.Morning),
                    CreateSubmission(yossi, date, ShiftKind.Afternoon)
                ],
                TotalEffectiveTargetHours: 8,
                ManagerConstraintSet: managerConstraintSet));

        Assert.Contains(
            buildResult.Problem.AvailabilityWindows,
            window =>
                window.ResourceId == dana.Id &&
                window.Covers(morningShift));

        Assert.DoesNotContain(
            buildResult.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == dana.Id &&
                preference.Type == ResourcePreferenceType.Prefer &&
                preference.StartUtc < morningShift.EndUtc &&
                morningShift.StartUtc < preference.EndUtc);

        Assert.Contains(
            buildResult.Problem.ResourcePreferences,
            preference =>
                preference.ResourceId == dana.Id &&
                preference.Type == ResourcePreferenceType.Avoid &&
                preference.Priority == ResourcePreferencePriority.High &&
                preference.StartUtc == morningShift.StartUtc &&
                preference.EndUtc == morningShift.EndUtc);

        var danaDemand = buildResult.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == dana.Id);

        var yossiDemand = buildResult.Problem.ResourceWorkloadDemands.Single(
            demand => demand.ResourceId == yossi.Id);

        Assert.Equal(0, danaDemand.RequestedPreferredHours);
        Assert.Equal(8, yossiDemand.RequestedPreferredHours);
    }

    [Fact]
    public void Build_ShouldMakeEvaluatorReportIgnoredAvoid_WhenManagerAvoidedWorkerIsAssigned()
    {
        var dana = CreateResource(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "Dana");

        var date = new DateOnly(2026, 6, 15);

        var morningShift = CreateShift(
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            date,
            ShiftKind.Morning);

        var period = new SchedulePeriod(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var managerConstraintSet = new ManagerConstraintSet(
            avoidAssignments:
            [
                new ManagerAvoidAssignment(
                    dana.Id,
                    morningShift.Id)
            ]);

        var buildResult = new FormSubmissionSchedulingProblemBuilder()
            .Build(new FormSubmissionSchedulingProblemBuildRequest(
                period,
                [dana],
                [morningShift],
                [
                    CreateSubmission(dana, date, ShiftKind.Morning)
                ],
                ManagerConstraintSet: managerConstraintSet));

        var candidate = new ScheduleCandidate([
            new Assignment(dana.Id, morningShift.Id)
        ]);

        var evaluation = new ScheduleEvaluator()
            .Evaluate(
                buildResult.Problem,
                candidate);

        Assert.True(evaluation.IsFeasible);

        var ignoredAvoidViolation = Assert.Single(
            evaluation.Violations.Where(
                violation => violation.Type == ConstraintViolationType.IgnoredAvoidPreference));

        Assert.Equal(ConstraintViolationSeverity.Soft, ignoredAvoidViolation.Severity);
        Assert.Equal(dana.Id, ignoredAvoidViolation.ResourceId);
        Assert.Equal(morningShift.Id, ignoredAvoidViolation.ShiftId);

        Assert.DoesNotContain(
            evaluation.Violations,
            violation => violation.Type == ConstraintViolationType.ResourceRequestedPreferredHoursNotSatisfied);
    }

    private static WorkerSubmission CreateSubmission(
        Resource resource,
        DateOnly date,
        ShiftKind shiftKind)
    {
        return new WorkerSubmission(
            resource.Id,
            [
                new WorkerShiftSubmission(
                    date,
                    shiftKind,
                    ShiftSubmissionChoice.StrongAvailable)
            ]);
    }

    private static Resource CreateResource(
        string id,
        string name)
    {
        return new Resource(
            Guid.Parse(id),
            name,
            hourlyCost: 0);
    }

    private static Shift CreateShift(
        string id,
        DateOnly date,
        ShiftKind kind)
    {
        return new Shift(
            Guid.Parse(id),
            GetStartUtc(date, kind),
            GetEndUtc(date, kind),
            kind,
            minResourceCount: 0,
            maxResourceCount: 4);
    }

    private static DateTime GetStartUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Night => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }

    private static DateTime GetEndUtc(DateOnly date, ShiftKind kind)
    {
        return kind switch
        {
            ShiftKind.Morning => date.ToDateTime(new TimeOnly(14, 20), DateTimeKind.Utc),
            ShiftKind.Afternoon => date.ToDateTime(new TimeOnly(22, 40), DateTimeKind.Utc),
            ShiftKind.Night => date.AddDays(1).ToDateTime(new TimeOnly(6, 30), DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Shift kind is not supported.")
        };
    }
}
