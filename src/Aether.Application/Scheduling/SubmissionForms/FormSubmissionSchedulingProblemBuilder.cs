using Aether.Application.Scheduling.ManagerConstraints;
using Aether.Application.Scheduling.Builders;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.SubmissionForms;

public sealed class FormSubmissionSchedulingProblemBuilder
{
    public FormSubmissionSchedulingProblemBuildResult Build(
        FormSubmissionSchedulingProblemBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyCollection<Shift> shifts = request.Shifts;

        if (request.ManagerConstraintSet is not null)
        {
            shifts = new ManagerConstraintApplicator()
                .ApplyToShifts(
                    shifts,
                    request.ManagerConstraintSet);
        }

        var aggregator = new WorkerSubmissionAggregator();

        var aggregationResult = aggregator.Aggregate(
            request.Period,
            request.Resources,
            shifts,
            request.WorkerSubmissions);

        var availabilityWindows = aggregationResult.AvailabilityWindows;
        var resourcePreferences = aggregationResult.ResourcePreferences;

        if (request.ApplyMandatoryShiftAvailabilityPolicy)
        {
            var policyResult = new MandatoryShiftAvailabilityPolicy()
                .Apply(
                    request.Resources,
                    shifts,
                    availabilityWindows,
                    resourcePreferences);

            availabilityWindows = policyResult.AvailabilityWindows;
            resourcePreferences = policyResult.ResourcePreferences;
        }

        if (request.ManagerConstraintSet is not null)
        {
            var managerConstraintResult = new ManagerConstraintApplicator()
                .Apply(
                    request.Resources,
                    shifts,
                    availabilityWindows,
                    resourcePreferences,
                    request.ManagerConstraintSet);

            availabilityWindows = managerConstraintResult.AvailabilityWindows;
            resourcePreferences = managerConstraintResult.ResourcePreferences;
        }

        var workloadDemands = CreateWorkloadDemands(
            request,
            shifts,
            resourcePreferences);

        var problem = new SchedulingProblem(
            period: request.Period,
            resources: request.Resources,
            shifts: shifts,
            availabilityWindows: availabilityWindows,
            resourcePreferences: resourcePreferences,
            minimumAssignedHoursPerResource: request.MinimumAssignedHoursPerResource,
            minimumMorningShiftsPerResourcePerFullWeek: request.MinimumMorningShiftsPerResourcePerFullWeek,
            minimumAfternoonShiftsPerResourcePerFullWeek: request.MinimumAfternoonShiftsPerResourcePerFullWeek,
            resourceMonthlyNightShiftHistories: request.ResourceMonthlyNightShiftHistories,
            maximumAssignedHoursDeviationFromAverageHours: request.MaximumAssignedHoursDeviationFromAverageHours,
            resourceWorkloadDemands: workloadDemands);

        return new FormSubmissionSchedulingProblemBuildResult(
            problem,
            aggregationResult.Warnings);
    }

    private static IReadOnlyCollection<ResourceWorkloadDemand> CreateWorkloadDemands(
        FormSubmissionSchedulingProblemBuildRequest request,
        IReadOnlyCollection<Shift> shifts,
        IReadOnlyCollection<ResourcePreference> resourcePreferences)
    {
        if (request.TotalEffectiveTargetHours is null)
        {
            return Array.Empty<ResourceWorkloadDemand>();
        }

        var preferPreferences = resourcePreferences
            .Where(preference => preference.Type == ResourcePreferenceType.Prefer)
            .ToArray();

        var builder = new ProportionalWorkloadDemandBuilder();

        return builder.Build(
            request.Resources,
            shifts,
            preferPreferences,
            request.TotalEffectiveTargetHours.Value);
    }
}
