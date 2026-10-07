using Core.DDD.Repositories;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Domain.MathStepGrading;
using MediatR;

namespace Elmanhg.Application.MathStepGrading.GetMathStepGrade;

public sealed class GetMathStepGradeHandler(IMathStepGradeRepository mathStepGradeRepository, ICurrentUserService currentUserService) : IRequestHandler<GetMathStepGradeQuery, MathStepGradeResult>
{
    public async Task<MathStepGradeResult> Handle(GetMathStepGradeQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var grade = await mathStepGradeRepository.GetRequiredAsync(x => x.SessionId == request.SessionId && x.QuestionId == request.QuestionId && x.StudentId == userId, ErrorCodes.MathStepGradeNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return MathStepGradeResultGenerator.Generate(grade);
    }
}
