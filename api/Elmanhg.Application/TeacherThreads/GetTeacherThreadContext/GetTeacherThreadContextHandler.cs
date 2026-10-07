using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.GetTeacherThreadContext;

public sealed class GetTeacherThreadContextHandler(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetTeacherThreadContextQuery, TeacherThreadContextResult>
{
    public async Task<TeacherThreadContextResult> Handle(GetTeacherThreadContextQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (!entitlement.HasAskTeacher)
        {
            throw new ForbiddenCoreException(ErrorCodes.AskTeacherRequiresSubscription);
        }

        var context = await TeacherThreadContextResolver.ResolveAsync(request.LessonId, request.QuestionId, request.AttemptId, userId, new TeacherThreadContextSources(lessonRepository, unitRepository, subjectRepository, questionRepository, sessionRepository), cancellationToken).ConfigureAwait(false);
        return TeacherThreadResultGenerator.GenerateContext(context);
    }
}
