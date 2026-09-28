using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.QuestionValidation.GetValidationQueue;

public sealed class GetValidationQueueHandler(IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ITeacherSubjectRepository teacherSubjectRepository, IReviewSessionRepository reviewSessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetValidationQueueQuery, PageData<ValidationQueueItemResult>>
{
    public async Task<PageData<ValidationQueueItemResult>> Handle(GetValidationQueueQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var assignments = await teacherSubjectRepository.FindAsync(x => x.TeacherId == userId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var subjectIds = assignments
            .Select(x => x.SubjectId)
            .ToList();
        var session = await ReadSessionAsync(request.ReviewSessionId, userId, cancellationToken).ConfigureAwait(false);
        List<Guid>? unitLessonIds = null;
        if (request.UnitId.HasValue)
        {
            var unitId = request.UnitId.Value;
            var unitLessons = await lessonRepository.FindAsync(x => x.UnitId == unitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            unitLessonIds = unitLessons
                .Select(x => x.Id)
                .ToList();
        }

        DateTimeOffset? submittedBefore = request.MinAgeDays is null ? null : DateTimeOffset.UtcNow.AddDays(-request.MinAgeDays.Value);
        var page = await questionRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: ValidationQueueFilter.Build(subjectIds, unitLessonIds, request, submittedBefore), orderBy: query => query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var lessonIds = page.Items
            .Select(x => x.LessonId)
            .Distinct()
            .ToList();
        var lessons = await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var unitIds = lessons
            .Select(x => x.UnitId)
            .Distinct()
            .ToList();
        var units = await unitRepository.FindAsync(x => unitIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var lessonsById = lessons.ToDictionary(x => x.Id);
        var unitsById = units.ToDictionary(x => x.Id);

        return new PageData<ValidationQueueItemResult>
        {
            Items = page.Items
                .Select(x => Generate(x, lessonsById, unitsById, session))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }

    private async Task<ReviewSession?> ReadSessionAsync(Guid? reviewSessionId, Guid userId, CancellationToken cancellationToken)
    {
        if (!reviewSessionId.HasValue)
        {
            return null;
        }

        var session = await reviewSessionRepository.GetByIdAsync(reviewSessionId.Value, cancellationToken, include: query => query.Include(x => x.Openings), asNoTracking: true).ConfigureAwait(false);
        if (session is null || session.TeacherId != userId)
        {
            throw new NotFoundCoreException(ErrorCodes.ReviewSessionNotFound);
        }

        return session;
    }

    private static ValidationQueueItemResult Generate(Question question, Dictionary<Guid, Lesson> lessonsById, Dictionary<Guid, CurriculumUnit> unitsById, ReviewSession? session)
    {
        var lesson = lessonsById.GetValueOrDefault(question.LessonId);
        var unit = lesson is null ? null : unitsById.GetValueOrDefault(lesson.UnitId);
        return ValidationResultGenerator.GenerateQueueItem(question, lesson, unit, session?.HasOpened(question) == true);
    }
}
