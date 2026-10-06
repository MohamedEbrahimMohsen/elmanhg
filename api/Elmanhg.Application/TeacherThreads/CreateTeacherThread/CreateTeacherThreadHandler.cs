using Core.Identity.Tokens.CurrentUser;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.CreateTeacherThread;

public sealed class CreateTeacherThreadHandler(ITeacherThreadRepository teacherThreadRepository, ISubscriptionRepository subscriptionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, IFileStorage fileStorage, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings, IExamPeriodRepository examPeriodRepository) : IRequestHandler<CreateTeacherThreadCommand, TeacherThreadResult>
{
    public async Task<TeacherThreadResult> Handle(CreateTeacherThreadCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var now = timeProvider.GetUtcNow();
        var options = subscriptionsOptions.Value;
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, options, now, cancellationToken).ConfigureAwait(false);
        await AskTeacherGate.EnsureCanAskAsync(entitlement, userId, teacherThreadRepository, options, now, cancellationToken).ConfigureAwait(false);
        var context = await TeacherThreadContextResolver.ResolveAsync(request.LessonId, request.QuestionId, request.AttemptId, userId, new TeacherThreadContextSources(lessonRepository, unitRepository, subjectRepository, questionRepository, sessionRepository), cancellationToken).ConfigureAwait(false);
        var imageUrl = request.Image is { } image ? await SaveImageAsync(image, cancellationToken).ConfigureAwait(false) : null;
        var slaPolicy = await TeacherThreadSlaPolicyLoader.LoadAsync(runtimeSettings, examPeriodRepository, cancellationToken).ConfigureAwait(false);
        var thread = TeacherThread.Submit(userId, context, request.Text ?? string.Empty, imageUrl, now, slaPolicy);

        await teacherThreadRepository.AddAsync(thread, cancellationToken).ConfigureAwait(false);
        await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TeacherThreadResultGenerator.Generate(thread, now);
    }

    private async Task<string> SaveImageAsync(IFormFile image, CancellationToken cancellationToken)
    {
        var key = $"{TeacherThreadImageFormats.StorageFolder}/{Guid.NewGuid():N}{Path.GetExtension(image.FileName).ToLowerInvariant()}";
        await using var content = image.OpenReadStream();
        return await fileStorage.SaveAsync(content, key, cancellationToken).ConfigureAwait(false);
    }
}
