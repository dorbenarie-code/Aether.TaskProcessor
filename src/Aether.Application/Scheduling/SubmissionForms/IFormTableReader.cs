namespace Aether.Application.Scheduling.SubmissionForms;

public interface IFormTableReader
{
    IReadOnlyList<IReadOnlyList<string>> Read(Stream stream);
}
