using MediatR;

namespace Elmanhg.Application.MathStepGrading.GradeMathSteps;

public sealed record GradeMathStepsCommand(Guid MathStepGradeId) : IRequest;
