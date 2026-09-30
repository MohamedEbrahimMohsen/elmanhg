namespace Elmanhg.Infrastructure.TrainingData;

public sealed class TrainingDataOptions
{
    public const string SectionName = "TrainingData";
    public const int MinStudentIdHashKeyLength = 32;
    // Only Development and Testing may fall back to this public key; the validator refuses it elsewhere.
    public const string DevelopmentStudentIdHashKey = "elmanhg-development-training-student-id-key-not-a-secret";

    public string StudentIdHashKey { get; set; } = string.Empty;
}
