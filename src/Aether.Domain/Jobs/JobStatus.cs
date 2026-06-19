namespace Aether.Domain.Jobs;

public enum JobStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}