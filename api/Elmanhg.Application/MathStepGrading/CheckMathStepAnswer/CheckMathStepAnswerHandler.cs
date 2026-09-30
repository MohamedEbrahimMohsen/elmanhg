using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared.Grading;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using MediatR;

namespace Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;

public sealed class CheckMathStepAnswerHandler(IMathStepGradeRepository mathStepGradeRepository, IQuestionRepository questionRepository, IAiMathCheckClient mathCheckClient, TimeProvider timeProvider) : IRequestHandler<CheckMathStepAnswerCommand>
{
    public async Task Handle(CheckMathStepAnswerCommand request, CancellationToken cancellationToken)
    {
        var grade = await mathStepGradeRepository.FirstOrDefaultAsync(x => x.Id == request.MathStepGradeId, cancellationToken).ConfigureAwait(false);
        if (grade is null || !grade.IsDueAt(timeProvider.GetUtcNow()) || grade.FinalAnswerVerdict is not null)
        {
            return;
        }

        var revisions = await questionRepository.GetRevisionsAsync([grade.QuestionId], cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(x => x.Version == grade.QuestionVersion) ?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        var gradingSpec = revision.ReadSnapshot().GradingSpec?.ToJsonString() ?? "{}";
        var verdict = await AnswerGrader.CheckFinalAnswerAsync(gradingSpec, grade.ReadAnswer().FinalAnswer!.Trim(), mathCheckClient, cancellationToken).ConfigureAwait(false);
        if (verdict == MathAnswerVerdict.Unchecked)
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.MathCheckUnavailable);
        }

        grade.RecordVerdict(verdict, timeProvider.GetUtcNow());

        await mathStepGradeRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
