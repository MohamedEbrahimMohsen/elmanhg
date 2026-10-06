using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.QuestionValidation.GetValidationQuestion;

public sealed class GetValidationQuestionHandler(IQuestionRepository questionRepository, ITeacherSubjectRepository teacherSubjectRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IUserRepository userRepository, IFileStorage fileStorage, ICurrentUserService currentUserService) : IRequestHandler<GetValidationQuestionQuery, ValidationQuestionDetailResult>
{
    public async Task<ValidationQuestionDetailResult> Handle(GetValidationQuestionQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var question = await questionRepository.GetRequiredAsync(request.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, include: query => query.Include(x => x.Revisions).Include(x => x.Decisions), asNoTracking: true).ConfigureAwait(false);

        if (!await teacherSubjectRepository.IsAssignedAsync(userId, question.SubjectId, cancellationToken).ConfigureAwait(false))
        {
            throw new ForbiddenCoreException(ErrorCodes.SubjectOutOfScope);
        }

        var lesson = await lessonRepository.GetWithObjectivesAsync(question.LessonId, asNoTracking: true, cancellationToken).ConfigureAwait(false);
        if (lesson is null)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var unit = await unitRepository.GetByIdAsync(lesson.UnitId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var subject = await subjectRepository.GetByIdAsync(question.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var deciderIds = question.Decisions
            .Select(x => x.DecidedBy)
            .Distinct()
            .ToList();
        var deciders = await userRepository.FindAsync(x => deciderIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var deciderNames = deciders.ToDictionary(x => x.Id, x => x.DisplayName);

        return ValidationResultGenerator.GenerateDetail(question, subject, unit, lesson, deciderNames, fileStorage);
    }
}
