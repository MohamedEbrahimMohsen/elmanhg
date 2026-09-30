using Core.DDD.Models;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetGradeReviewQueue;

public sealed class GetGradeReviewQueueHandler(IEssayGradeRepository essayGradeRepository, IMathStepGradeRepository mathStepGradeRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository) : IRequestHandler<GetGradeReviewQueueQuery, PageData<GradeReviewItemResult>>
{
    public async Task<PageData<GradeReviewItemResult>> Handle(GetGradeReviewQueueQuery request, CancellationToken cancellationToken) => request.Kind switch
    {
        GradeReviewKind.Essay => await EssaysAsync(request, cancellationToken).ConfigureAwait(false),
        GradeReviewKind.MathSteps => await MathStepsAsync(request, cancellationToken).ConfigureAwait(false),
        _ => throw new BadRequestCoreException(ErrorCodes.GradeReviewKindInvalid),
    };

    private async Task<PageData<GradeReviewItemResult>> EssaysAsync(GetGradeReviewQueueQuery request, CancellationToken cancellationToken)
    {
        var page = await essayGradeRepository.GetInReviewPageAsync(request.SubjectId, request.PageNumber, request.PageSize, cancellationToken).ConfigureAwait(false);
        var (questions, placements) = await LoadAsync(page.Items.Select(x => x.QuestionId), cancellationToken).ConfigureAwait(false);
        var items = page.Items
            .Select(x => GradeReviewResultGenerator.Item(x, questions.GetValueOrDefault(x.QuestionId), placements.GetValueOrDefault(x.QuestionId) ?? GradeReviewPlacement.Unknown))
            .ToList();
        return Page(items, page);
    }

    private async Task<PageData<GradeReviewItemResult>> MathStepsAsync(GetGradeReviewQueueQuery request, CancellationToken cancellationToken)
    {
        var page = await mathStepGradeRepository.GetInReviewPageAsync(request.SubjectId, request.PageNumber, request.PageSize, cancellationToken).ConfigureAwait(false);
        var (questions, placements) = await LoadAsync(page.Items.Select(x => x.QuestionId), cancellationToken).ConfigureAwait(false);
        var items = page.Items
            .Select(x => GradeReviewResultGenerator.Item(x, questions.GetValueOrDefault(x.QuestionId), placements.GetValueOrDefault(x.QuestionId) ?? GradeReviewPlacement.Unknown))
            .ToList();
        return Page(items, page);
    }

    private Task<(Dictionary<Guid, Question> Questions, Dictionary<Guid, GradeReviewPlacement> Placements)> LoadAsync(IEnumerable<Guid> questionIds, CancellationToken cancellationToken)
    {
        var ids = questionIds
            .Distinct()
            .ToList();
        return GradeReviewPlacementLoader.LoadAsync(ids, questionRepository, lessonRepository, unitRepository, cancellationToken);
    }

    private static PageData<GradeReviewItemResult> Page<T>(List<GradeReviewItemResult> items, PageData<T> page) => new()
    {
        Items = items,
        PageNumber = page.PageNumber,
        PageSize = page.PageSize,
        TotalItems = page.TotalItems,
        TotalPages = page.TotalPages,
    };
}
