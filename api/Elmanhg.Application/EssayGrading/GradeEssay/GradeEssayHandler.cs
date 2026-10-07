using Core.DDD.Repositories;
using Core.Errors;
using Core.Settings;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.EssayGrading.GradeEssay;

public sealed class GradeEssayHandler(IEssayGradeRepository essayGradeRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IRichTextExtractor richTextExtractor, IAiEssayGradingClient essayGradingClient, IOptions<EssayGradingOptions> essayGradingOptions, TimeProvider timeProvider, IRuntimeSettings runtimeSettings) : IRequestHandler<GradeEssayCommand>
{
    public async Task Handle(GradeEssayCommand request, CancellationToken cancellationToken)
    {
        var grade = await essayGradeRepository.FirstOrDefaultAsync(x => x.Id == request.EssayGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || !grade.IsDueAt(timeProvider.GetUtcNow()))
        {
            return;
        }

        var revisions = await questionRepository.GetRevisionsAsync([grade.QuestionId], cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(x => x.Version == grade.QuestionVersion) ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        var question = await questionRepository.GetRequiredAsync(x => x.Id == grade.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var context = await EssayGradingContextLoader.LoadAsync(question.LessonId, lessonRepository, unitRepository, subjectRepository, cancellationToken).ConfigureAwait(false);

        var options = essayGradingOptions.Value;
        var snapshot = revision.ReadSnapshot();
        var aiRequest = EssayGradingRequestFactory.Create(snapshot.Stem, snapshot.GradingSpec?.ToJsonString() ?? "{}", grade.ReadAnswerText(), context, richTextExtractor, options.ContextFieldMaxLength);
        var result = await essayGradingClient.GradeAsync(aiRequest, cancellationToken).ConfigureAwait(false);
        var questionGrade = revision.GradeEssay(EssayAssessments.Awards(result));
        grade.Complete(EssayAssessments.From(aiRequest, result), questionGrade, await runtimeSettings.GetAsync(GradingRuntimeSettings.EssayReviewConfidenceThreshold, cancellationToken).ConfigureAwait(false), timeProvider.GetUtcNow());

        await essayGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
