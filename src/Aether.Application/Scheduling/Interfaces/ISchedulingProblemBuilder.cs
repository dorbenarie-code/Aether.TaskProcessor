using Aether.Application.Scheduling.Contracts;

namespace Aether.Application.Scheduling.Interfaces;

public interface ISchedulingProblemBuilder
{
    SchedulingProblemBuildResult Build(SchedulingProblemBuildRequest request);
}
