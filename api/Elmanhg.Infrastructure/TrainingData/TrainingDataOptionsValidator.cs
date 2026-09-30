using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.TrainingData;

public sealed class TrainingDataOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<TrainingDataOptions>
{
    private const string TestingEnvironment = "Testing";

    public ValidateOptionsResult Validate(string? name, TrainingDataOptions options)
    {
        var key = options.StudentIdHashKey ?? string.Empty;
        var requiresKey = !hostEnvironment.IsDevelopment() && !hostEnvironment.IsEnvironment(TestingEnvironment);
        List<string> failures = [];
        if (key.Length > 0 && key.Length < TrainingDataOptions.MinStudentIdHashKeyLength)
        {
            failures.Add("TrainingData:StudentIdHashKey must be at least 32 characters.");
        }

        if (requiresKey && key.Length == 0)
        {
            failures.Add("TrainingData:StudentIdHashKey is required outside Development and Testing.");
        }

        if (requiresKey && key == TrainingDataOptions.DevelopmentStudentIdHashKey)
        {
            failures.Add("TrainingData:StudentIdHashKey must not be the development key outside Development and Testing.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
