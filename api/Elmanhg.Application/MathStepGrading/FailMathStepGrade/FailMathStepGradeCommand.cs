using Elmanhg.Application.Shared.Retries;

namespace Elmanhg.Application.MathStepGrading.FailMathStepGrade;

public sealed record FailMathStepGradeCommand(Guid MathStepGradeId, string ErrorCode) : IFailRetriedWorkCommand
{
    Guid IFailRetriedWorkCommand.WorkId => MathStepGradeId;
}
