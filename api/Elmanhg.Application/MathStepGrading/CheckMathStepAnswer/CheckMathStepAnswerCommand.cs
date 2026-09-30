using MediatR;

namespace Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;

public sealed record CheckMathStepAnswerCommand(Guid MathStepGradeId) : IRequest;
