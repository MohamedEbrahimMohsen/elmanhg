using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetContentMetrics;

public sealed class GetContentMetricsHandler(ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, TimeProvider timeProvider) : IRequestHandler<GetContentMetricsQuery, ContentMetricsResult>
{
    public async Task<ContentMetricsResult> Handle(GetContentMetricsQuery request, CancellationToken cancellationToken)
    {
        await DashboardSubjectGuard.EnsureExistsAsync(request.SubjectId, subjectRepository, cancellationToken).ConfigureAwait(false);
        var subjects = request.SubjectId is null ? await subjectRepository.CountAsync(cancellationToken).ConfigureAwait(false) : 1;
        var units = await unitRepository.CountAsync(cancellationToken, x => request.SubjectId == null || x.SubjectId == request.SubjectId).ConfigureAwait(false);
        var lessons = await lessonRepository.CountByStateAsync(request.SubjectId, cancellationToken).ConfigureAwait(false);
        var inventory = await questionRepository.CountInventoryAsync(request.SubjectId, cancellationToken).ConfigureAwait(false);
        var servable = request.SubjectId is { } subjectId ? await questionRepository.CountServableInSubjectAsync(subjectId, cancellationToken).ConfigureAwait(false) : await questionRepository.CountServableAsync(cancellationToken).ConfigureAwait(false);

        return ContentMetricsResultGenerator.Generate(request.SubjectId, subjects, units, lessons, inventory, servable, timeProvider.GetUtcNow());
    }
}
