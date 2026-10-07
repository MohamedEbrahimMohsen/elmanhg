using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.GetQuestions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Questions.GetQuestions;

public sealed class GetQuestionsValidatorTests
{
    private readonly GetQuestionsValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100, QuestionListMaxPageSize = 100, QuestionFilterMaxLength = 200 }));

    [Fact]
    public void Validate_DefaultQuery_HasNoErrors()
    {
        _validator.Validate(Query()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_HasQuestionPageNumberInvalid()
    {
        Codes(Query() with { PageNumber = 0 }).Should().Contain(ErrorCodes.QuestionPageNumberInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PageSizeOutOfRange_HasQuestionPageSizeInvalid(int pageSize)
    {
        Codes(Query() with { PageSize = pageSize }).Should().Contain(ErrorCodes.QuestionPageSizeInvalid);
    }

    [Fact]
    public void Validate_UndefinedStatus_HasQuestionStatusInvalid()
    {
        Codes(Query() with { Status = (QuestionValidationStatus)9 }).Should().Contain(ErrorCodes.QuestionStatusInvalid);
    }

    [Fact]
    public void Validate_UndefinedType_HasQuestionTypeInvalid()
    {
        Codes(Query() with { Type = (QuestionType)99 }).Should().Contain(ErrorCodes.QuestionTypeInvalid);
    }

    [Fact]
    public void Validate_MinVersionZero_HasQuestionVersionFilterInvalid()
    {
        Codes(Query() with { MinVersion = 0 }).Should().Contain(ErrorCodes.QuestionVersionFilterInvalid);
    }

    [Fact]
    public void Validate_RejectionReasonOverCap_HasQuestionFilterTooLong()
    {
        Codes(Query() with { RejectionReason = new string('a', 201) }).Should().Contain(ErrorCodes.QuestionFilterTooLong);
    }

    [Fact]
    public void Validate_PageOffsetPastIntRange_FailsPageNumberInvalid()
    {
        Codes(Query() with { PageNumber = int.MaxValue, PageSize = 20 }).Should().Contain(ErrorCodes.QuestionPageNumberInvalid);
    }

    private static GetQuestionsQuery Query() => new(null, null, null, null, null, null, null);

    private List<string> Codes(GetQuestionsQuery query)
    {
        return _validator.Validate(query).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
