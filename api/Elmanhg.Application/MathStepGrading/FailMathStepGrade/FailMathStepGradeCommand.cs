using MediatR;

namespace Elmanhg.Application.MathStepGrading.FailMathStepGrade;

public sealed record FailMathStepGradeCommand(Guid MathStepGradeId, string ErrorCode) : IRequest;
