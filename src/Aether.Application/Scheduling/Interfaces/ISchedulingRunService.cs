using Aether.Application.Scheduling.Contracts;

namespace Aether.Application.Scheduling.Interfaces;

public interface ISchedulingRunService
{
    SchedulingRunResult Run(SchedulingProblemBuildRequest request);
}
