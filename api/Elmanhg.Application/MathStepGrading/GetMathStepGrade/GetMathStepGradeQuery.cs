using Elmanhg.Application.MathStepGrading.Shared;
using MediatR;

namespace Elmanhg.Application.MathStepGrading.GetMathStepGrade;

public sealed record GetMathStepGradeQuery(Guid SessionId, Guid QuestionId) : IRequest<MathStepGradeResult>;
