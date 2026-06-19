using Aether.Domain.Optimization;

namespace Aether.Tests.Domain.Optimization;

public sealed class ResourceWorkloadPolicyTests
{
    [Fact]
    public void Full_policy_requires_minimum_90_assigned_hours()
    {
        var policy = ResourceWorkloadPolicy.CreateFullPolicy();

        Assert.Equal(ResourceWorkloadCategory.Full, policy.Category);
        Assert.Equal(90, policy.MinimumRequiredAssignedHours);
    }

    [Fact]
    public void Full_policy_does_not_use_preferred_target_assigned_hours()
    {
        var policy = ResourceWorkloadPolicy.CreateFullPolicy();

        Assert.Null(policy.PreferredTargetAssignedHours);
        Assert.False(policy.PenalizeAssignedHoursAbovePreferredTarget);
    }

    [Fact]
    public void Student_policy_requires_minimum_90_assigned_hours()
    {
        var policy = ResourceWorkloadPolicy.CreateStudentPolicy();

        Assert.Equal(ResourceWorkloadCategory.Student, policy.Category);
        Assert.Equal(90, policy.MinimumRequiredAssignedHours);
    }

    [Fact]
    public void Student_policy_uses_90_as_preferred_target_assigned_hours()
    {
        var policy = ResourceWorkloadPolicy.CreateStudentPolicy();

        Assert.Equal(90, policy.PreferredTargetAssignedHours);
    }

    [Fact]
    public void Student_policy_penalizes_hours_above_preferred_target()
    {
        var policy = ResourceWorkloadPolicy.CreateStudentPolicy();

        Assert.True(policy.PenalizeAssignedHoursAbovePreferredTarget);
    }

    [Fact]
    public void Special_policy_is_not_subject_to_90_minimum_assigned_hours()
    {
        var policy = ResourceWorkloadPolicy.CreateSpecialPolicy();

        Assert.Equal(ResourceWorkloadCategory.Special, policy.Category);
        Assert.Equal(0, policy.MinimumRequiredAssignedHours);
    }

    [Fact]
    public void Special_policy_uses_66_as_preferred_target_assigned_hours()
    {
        var policy = ResourceWorkloadPolicy.CreateSpecialPolicy();

        Assert.Equal(66, policy.PreferredTargetAssignedHours);
    }

    [Fact]
    public void Special_policy_penalizes_hours_above_preferred_target()
    {
        var policy = ResourceWorkloadPolicy.CreateSpecialPolicy();

        Assert.True(policy.PenalizeAssignedHoursAbovePreferredTarget);
    }

    [Fact]
    public void Workload_policy_rejects_unsupported_category()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadPolicy(
                (ResourceWorkloadCategory)999,
                minimumRequiredAssignedHours: 90,
                preferredTargetAssignedHours: 90,
                penalizeAssignedHoursAbovePreferredTarget: true));

        Assert.Equal("category", exception.ParamName);
    }

    [Fact]
    public void Workload_policy_rejects_negative_minimum_required_assigned_hours()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadPolicy(
                ResourceWorkloadCategory.Student,
                minimumRequiredAssignedHours: -1,
                preferredTargetAssignedHours: 90,
                penalizeAssignedHoursAbovePreferredTarget: true));

        Assert.Equal("minimumRequiredAssignedHours", exception.ParamName);
    }

    [Fact]
    public void Workload_policy_rejects_negative_preferred_target_assigned_hours()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ResourceWorkloadPolicy(
                ResourceWorkloadCategory.Student,
                minimumRequiredAssignedHours: 90,
                preferredTargetAssignedHours: -1,
                penalizeAssignedHoursAbovePreferredTarget: true));

        Assert.Equal("preferredTargetAssignedHours", exception.ParamName);
    }

    [Fact]
    public void Workload_policy_rejects_preferred_target_below_minimum_required_hours()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceWorkloadPolicy(
                ResourceWorkloadCategory.Student,
                minimumRequiredAssignedHours: 90,
                preferredTargetAssignedHours: 80,
                penalizeAssignedHoursAbovePreferredTarget: true));

        Assert.Equal("preferredTargetAssignedHours", exception.ParamName);
    }

    [Fact]
    public void Workload_policy_requires_preferred_target_when_over_target_penalty_is_enabled()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ResourceWorkloadPolicy(
                ResourceWorkloadCategory.Student,
                minimumRequiredAssignedHours: 90,
                preferredTargetAssignedHours: null,
                penalizeAssignedHoursAbovePreferredTarget: true));

        Assert.Equal("preferredTargetAssignedHours", exception.ParamName);
    }
}
