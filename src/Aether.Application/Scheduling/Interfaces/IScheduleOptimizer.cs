using Aether.Application.Scheduling.Contracts;
using Aether.Domain.Optimization;

namespace Aether.Application.Scheduling.Interfaces;

public interface IScheduleOptimizer
{
    ScheduleOptimizationResult Optimize(SchedulingProblem problem);
}
