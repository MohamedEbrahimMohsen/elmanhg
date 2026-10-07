using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.EssayGrading;
using MediatR;

namespace Elmanhg.Application.EssayGrading.GetEssayGrade;

public sealed class GetEssayGradeHandler(IEssayGradeRepository essayGradeRepository, ICurrentUserService currentUserService) : IRequestHandler<GetEssayGradeQuery, EssayGradeResult>
{
    public async Task<EssayGradeResult> Handle(GetEssayGradeQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var grade = await essayGradeRepository.GetRequiredAsync(x => x.SessionId == request.SessionId && x.QuestionId == request.QuestionId && x.StudentId == userId, ErrorCodes.EssayGradeNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return EssayGradeResultGenerator.Generate(grade);
    }
}
