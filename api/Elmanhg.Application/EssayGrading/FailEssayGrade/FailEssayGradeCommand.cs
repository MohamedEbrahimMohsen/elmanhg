using Elmanhg.Application.Shared.Retries;

namespace Elmanhg.Application.EssayGrading.FailEssayGrade;

public sealed record FailEssayGradeCommand(Guid EssayGradeId, string ErrorCode) : IFailRetriedWorkCommand
{
    Guid IFailRetriedWorkCommand.WorkId => EssayGradeId;
}
