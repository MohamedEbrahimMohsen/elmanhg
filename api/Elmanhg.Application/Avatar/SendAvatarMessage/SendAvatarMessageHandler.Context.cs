using Core.Errors;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.ContentRetrieval.SearchLessonContent;
using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Avatar.SendAvatarMessage;

public sealed partial class SendAvatarMessageHandler
{
    private Task<AvatarContext> LoadContextAsync(SendAvatarMessageCommand request, Guid studentId, EntitlementResult entitlement, CancellationToken cancellationToken) => request.EntryPoint switch
    {
        AvatarEntryPoint.Lesson => LoadLessonContextAsync(request, studentId, entitlement, cancellationToken),
        AvatarEntryPoint.QuizQuestion => LoadQuestionContextAsync(request, studentId, entitlement, false, cancellationToken),
        AvatarEntryPoint.ExamReview => LoadQuestionContextAsync(request, studentId, entitlement, true, cancellationToken),
        _ => LoadGlobalContextAsync(cancellationToken),
    };

    private async Task<AvatarContext> LoadLessonContextAsync(SendAvatarMessageCommand request, Guid studentId, EntitlementResult entitlement, CancellationToken cancellationToken)
    {
        var lesson = await LoadPublishedLessonAsync(request.LessonId!.Value, cancellationToken).ConfigureAwait(false);
        await FreeTierGate.EnsureLessonOpenAsync(entitlement, lesson.Id, lessonRepository, cancellationToken).ConfigureAwait(false);
        var (unit, subject) = await LoadUnitAndSubjectAsync(lesson, cancellationToken).ConfigureAwait(false);
        var matches = await SearchAsync(lesson.Id, AvatarText.RetrievalQuery(request.Message, null, contentRetrievalOptions.Value.QueryMaxLength), studentId, cancellationToken).ConfigureAwait(false);
        return new AvatarContext(AvatarContextBundleFactory.ForLesson(AvatarEntryPoint.Lesson, subject, unit, lesson, null, matches.Count > 0, richTextExtractor, avatarOptions.Value.ContextFieldMaxLength), lesson.Id, matches);
    }

    private async Task<AvatarContext> LoadQuestionContextAsync(SendAvatarMessageCommand request, Guid studentId, EntitlementResult entitlement, bool exam, CancellationToken cancellationToken)
    {
        var sessionId = request.SessionId!.Value;
        var questionId = request.QuestionId!.Value;
        var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == sessionId && x.StudentId == studentId && (exam ? x.Kind != SessionKind.Quiz && x.SubmittedAt != null : x.Kind == SessionKind.Quiz), cancellationToken, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery(), asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.SessionNotFound);
        var item = session.GetItem(questionId) ?? throw new NotFoundCoreException(ErrorCodes.SessionQuestionNotFound);
        var attempt = session.FindAttempt(questionId);
        if (attempt is null && !exam)
        {
            throw new BadRequestCoreException(ErrorCodes.AvatarQuestionNotAnswered);
        }

        var revisions = await questionRepository.GetRevisionsAsync([questionId], cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(x => x.Version == item.QuestionVersion);
        var question = await questionRepository.GetByIdAsync(questionId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (revision is null || question is null)
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        var lesson = await LoadPublishedLessonAsync(question.LessonId, cancellationToken).ConfigureAwait(false);
        if (!exam)
        {
            await FreeTierGate.EnsureLessonOpenAsync(entitlement, lesson.Id, lessonRepository, cancellationToken).ConfigureAwait(false);
        }

        var (unit, subject) = await LoadUnitAndSubjectAsync(lesson, cancellationToken).ConfigureAwait(false);
        var fieldMaxLength = avatarOptions.Value.ContextFieldMaxLength;
        var questionContext = AvatarContextBundleFactory.ForQuestion(questionId, revision.ReadSnapshot(), attempt?.Answer, richTextExtractor, fieldMaxLength);
        var matches = await SearchAsync(lesson.Id, AvatarText.RetrievalQuery(request.Message, questionContext.Stem, contentRetrievalOptions.Value.QueryMaxLength), studentId, cancellationToken).ConfigureAwait(false);
        return new AvatarContext(AvatarContextBundleFactory.ForLesson(request.EntryPoint, subject, unit, lesson, questionContext, matches.Count > 0, richTextExtractor, fieldMaxLength), lesson.Id, matches);
    }

    private async Task<List<LessonContentMatchResult>> SearchAsync(Guid lessonId, string query, Guid studentId, CancellationToken cancellationToken)
    {
        var search = await sender.Send(new SearchLessonContentQuery(lessonId, query, null, true), cancellationToken).ConfigureAwait(false);
        var withheld = await AvatarWithheldQuestions.LoadAsync(studentId, sessionRepository, cancellationToken).ConfigureAwait(false);
        return AvatarWithheldQuestions.Filter(search.Matches, withheld);
    }

    private async Task<AvatarContext> LoadGlobalContextAsync(CancellationToken cancellationToken)
    {
        var subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order), asNoTracking: true).ConfigureAwait(false) ?? [];
        return new AvatarContext(AvatarContextBundleFactory.ForGlobal(subjects.Select(x => x.Name).ToList()), null, []);
    }

    private async Task<Lesson> LoadPublishedLessonAsync(Guid lessonId, CancellationToken cancellationToken)
    {
        var lesson = await lessonRepository.GetWithObjectivesAsync(lessonId, true, cancellationToken).ConfigureAwait(false);
        if (lesson is null || lesson.State != LessonState.Published)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        return lesson;
    }

    private async Task<(CurriculumUnit Unit, Subject Subject)> LoadUnitAndSubjectAsync(Lesson lesson, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(lesson.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        var subject = await subjectRepository.GetByIdAsync(unit.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        return (unit, subject);
    }
}
