using Core.DDD.Repositories;
using Core.Errors;
using Core.Settings;
using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Elmanhg.Application.MathStepGrading.GradeMathSteps;

public sealed class GradeMathStepsHandler(IMathStepGradeRepository mathStepGradeRepository, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IRichTextExtractor richTextExtractor, IAiMathStepGradingClient mathStepGradingClient, IOptions<MathStepGradingOptions> mathStepGradingOptions, TimeProvider timeProvider, IRuntimeSettings runtimeSettings) : IRequestHandler<GradeMathStepsCommand>
{
    public async Task Handle(GradeMathStepsCommand request, CancellationToken cancellationToken)
    {
        var grade = await mathStepGradeRepository.FirstOrDefaultAsync(x => x.Id == request.MathStepGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || !grade.IsDueAt(timeProvider.GetUtcNow()) || grade.FinalAnswerVerdict is not { } verdict)
        {
            return;
        }

        var revisions = await questionRepository.GetRevisionsAsync([grade.QuestionId], cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(x => x.Version == grade.QuestionVersion) ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        var snapshot = revision.ReadSnapshot();
        var spec = JsonSerializer.Deserialize<MathStepsGradingSpec>(snapshot.GradingSpec?.ToJsonString() ?? "{}", QuestionJson.SerializerOptions)!;
        var answer = grade.ReadAnswer();
        var options = mathStepGradingOptions.Value;
        MathStepAssessment? assessment = null;
        var questionGrade = revision.GradeMathSteps(verdict, null);
        if (MathStepsGrader.NeedsStepGrading(spec, answer))
        {
            var question = await questionRepository.GetRequiredAsync(x => x.Id == grade.QuestionId, ErrorCodes.QuestionNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
            var context = await EssayGradingContextLoader.LoadAsync(question.LessonId, lessonRepository, unitRepository, subjectRepository, cancellationToken).ConfigureAwait(false);
            var aiRequest = MathStepGradingRequestFactory.Create(snapshot.Stem, spec, answer, context, richTextExtractor, options.ContextFieldMaxLength);
            var result = await mathStepGradingClient.GradeAsync(aiRequest, cancellationToken).ConfigureAwait(false);
            assessment = MathStepAssessments.From(aiRequest, result);
            questionGrade = revision.GradeMathSteps(verdict, MathStepAssessments.Awards(result));
        }

        grade.Complete(assessment, questionGrade, await runtimeSettings.GetAsync(GradingRuntimeSettings.MathStepReviewConfidenceThreshold, cancellationToken).ConfigureAwait(false), timeProvider.GetUtcNow());

        await mathStepGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
