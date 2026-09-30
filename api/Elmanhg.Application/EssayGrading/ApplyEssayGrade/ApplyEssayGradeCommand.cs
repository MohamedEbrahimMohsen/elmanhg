using MediatR;

namespace Elmanhg.Application.EssayGrading.ApplyEssayGrade;

public sealed record ApplyEssayGradeCommand(Guid EssayGradeId) : IRequest;
