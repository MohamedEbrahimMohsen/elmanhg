using Core.Errors;
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
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var grade = await essayGradeRepository.FirstOrDefaultAsync(x => x.SessionId == request.SessionId && x.QuestionId == request.QuestionId && x.StudentId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.EssayGradeNotFound);
        return EssayGradeResultGenerator.Generate(grade);
    }
}
