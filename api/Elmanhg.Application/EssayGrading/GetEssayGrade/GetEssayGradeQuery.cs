using Elmanhg.Application.EssayGrading.Shared;
using MediatR;

namespace Elmanhg.Application.EssayGrading.GetEssayGrade;

public sealed record GetEssayGradeQuery(Guid SessionId, Guid QuestionId) : IRequest<EssayGradeResult>;
