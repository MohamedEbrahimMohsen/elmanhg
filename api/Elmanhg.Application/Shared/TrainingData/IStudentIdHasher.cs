namespace Elmanhg.Application.Shared.TrainingData;

public interface IStudentIdHasher
{
    string Hash(Guid studentId);

    string HashSourceId(string scope, Guid sourceId);
}
