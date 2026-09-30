using MediatR;

namespace Elmanhg.Application.EssayGrading.FailEssayGrade;

public sealed record FailEssayGradeCommand(Guid EssayGradeId, string ErrorCode) : IRequest;
