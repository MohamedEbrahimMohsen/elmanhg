using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetValidationMetrics;

public sealed class GetValidationMetricsHandler(IQuestionRepository questionRepository, IUserRepository userRepository, ISubjectRepository subjectRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IRequestHandler<GetValidationMetricsQuery, ValidationMetricsResult>
{
    public async Task<ValidationMetricsResult> Handle(GetValidationMetricsQuery request, CancellationToken cancellationToken)
    {
        await DashboardSubjectGuard.EnsureExistsAsync(request.SubjectId, subjectRepository, cancellationToken).ConfigureAwait(false);
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var backlog = await questionRepository.CountAsync(cancellationToken, x => x.ValidationStatus == QuestionValidationStatus.Pending && x.RetiredAt == null && (request.SubjectId == null || x.SubjectId == request.SubjectId)).ConfigureAwait(false);
        var stats = await questionRepository.GetDecisionStatsAsync(window.Start, window.End, request.SubjectId, null, cancellationToken).ConfigureAwait(false);
        var byTeacher = await questionRepository.CountDecisionsByTeacherAsync(window.Start, window.End, request.SubjectId, cancellationToken).ConfigureAwait(false);
        var teacherIds = byTeacher.Select(x => x.TeacherId).ToList();
        List<User> teachers = teacherIds.Count == 0 ? [] : await userRepository.FindAsync(x => teacherIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var daily = await questionRepository.CountDecisionsByDayAsync(window.ToMetricsWindow(), request.SubjectId, cancellationToken).ConfigureAwait(false);
        var names = teachers.ToDictionary(x => x.Id, x => x.DisplayName);
        var throughput = byTeacher
            .Select(x => new TeacherThroughputResult(x.TeacherId, names.GetValueOrDefault(x.TeacherId) ?? string.Empty, x.Approved, x.Rejected))
            .OrderByDescending(x => x.Approved + x.Rejected)
            .ThenBy(x => x.DisplayName, StringComparer.Ordinal)
            .ToList();

        return new ValidationMetricsResult(window.From, window.To, request.SubjectId, backlog, stats.Approved, stats.Rejected, DashboardRates.Seconds(stats.MedianSecondsToDecision), throughput, DashboardSeries.Fill(window, daily), window.Now);
    }
}
