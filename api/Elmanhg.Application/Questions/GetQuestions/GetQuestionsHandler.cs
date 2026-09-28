using Core.DDD.Models;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.Questions.GetQuestions;

public sealed class GetQuestionsHandler(IQuestionRepository questionRepository, ILessonRepository lessonRepository, IUserRepository userRepository) : IRequestHandler<GetQuestionsQuery, PageData<QuestionListItemResult>>
{
    public async Task<PageData<QuestionListItemResult>> Handle(GetQuestionsQuery request, CancellationToken cancellationToken)
    {
        var page = await questionRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: GetQuestionsFilter.Build(request), orderBy: query => query.OrderByDescending(x => x.UpdationDate).ThenByDescending(x => x.Id), asNoTracking: true).ConfigureAwait(false);
        var lessonIds = page.Items
            .Select(x => x.LessonId)
            .Distinct()
            .ToList();
        var teacherIds = page.Items
            .Where(x => x.ValidatedBy.HasValue)
            .Select(x => x.ValidatedBy.GetValueOrDefault())
            .Distinct()
            .ToList();

        var lessons = await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var teachers = await userRepository.FindAsync(x => teacherIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var lessonNames = lessons.ToDictionary(x => x.Id, x => x.Name);
        var teacherNames = teachers.ToDictionary(x => x.Id, x => x.DisplayName);

        return new PageData<QuestionListItemResult>
        {
            Items = page.Items
                .Select(x => QuestionResultGenerator.GenerateListItem(x, lessonNames.GetValueOrDefault(x.LessonId, string.Empty), x.ValidatedBy is null ? null : teacherNames.GetValueOrDefault(x.ValidatedBy.Value)))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
        };
    }
}
