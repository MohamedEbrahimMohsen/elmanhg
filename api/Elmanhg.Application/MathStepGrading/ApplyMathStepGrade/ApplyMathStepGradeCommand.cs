using MediatR;

namespace Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;

public sealed record ApplyMathStepGradeCommand(Guid MathStepGradeId) : IRequest;
