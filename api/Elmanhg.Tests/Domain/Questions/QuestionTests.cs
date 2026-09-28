using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionTests
{
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _editor = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_IsPendingVersionOneWithSubjectFromUnit()
    {
        var question = _builder.WithMetadata(new QuestionMetadata(QuestionDifficulty.Hard, null, ["arithmetic"])).Build();

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(1);
        question.SubjectId.Should().Be(_builder.Unit.SubjectId);
        question.LessonId.Should().Be(_builder.Lesson.Id);
        question.Type.Should().Be(QuestionType.Mcq);
        question.CurrentContent.Should().Be(QuestionBuilder.McqContent());
        (question.Difficulty, question.ObjectiveId).Should().Be((QuestionDifficulty.Hard, (Guid?)null));
        question.Tags.Should().Equal("arithmetic");
        question.ValidatedBy.Should().BeNull();
    }

    [Fact]
    public void Create_Always_AddsVersionOneRevisionSnapshot()
    {
        var creator = Guid.NewGuid();

        var question = Question.Create(_builder.Lesson, _builder.Unit, QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);

        var revision = question.Revisions.Should().ContainSingle().Subject;
        revision.Version.Should().Be(1);
        revision.EditedBy.Should().Be(creator);
        var snapshot = QuestionBuilder.Json(revision.Snapshot);
        snapshot.GetProperty("stem").GetString().Should().Be("<p>2 + 2 = ?</p>");
        snapshot.GetProperty("type").GetString().Should().Be("mcq");
        snapshot.GetProperty("body").GetProperty("options")[1].GetProperty("text").GetString().Should().Be("4");
    }

    [Fact]
    public void Create_ObjectiveOfLesson_LinksObjective()
    {
        var question = _builder.WithMetadata(new QuestionMetadata(QuestionDifficulty.Medium, _builder.ObjectiveId, [])).Build();

        question.ObjectiveId.Should().Be(_builder.ObjectiveId);
    }

    [Fact]
    public void Create_ObjectiveNotInLesson_ThrowsQuestionObjectiveNotInLesson()
    {
        var act = () => _builder.WithMetadata(new QuestionMetadata(QuestionDifficulty.Medium, Guid.NewGuid(), [])).Build();

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionObjectiveNotInLesson);
    }

    [Fact]
    public void Update_ContentChangeOnApproved_ResetsToPendingAndClearsValidation()
    {
        var question = _builder.Approved().Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.ValidatedBy.Should().BeNull();
        question.ValidatedAt.Should().BeNull();
    }

    [Fact]
    public void Update_ContentChangeOnApproved_IncrementsVersionAndAddsRevision()
    {
        var question = _builder.Approved().Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        question.Version.Should().Be(2);
        question.Revisions.Should().HaveCount(2);
        var last = question.Revisions[^1];
        last.Version.Should().Be(2);
        QuestionBuilder.Json(last.Snapshot).GetProperty("stem").GetString().Should().Be("<p>3 + 3 = ?</p>");
    }

    [Fact]
    public void Update_ContentChangeOnPending_IncrementsVersionAndStaysPending()
    {
        var question = _builder.Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Explanation = "<p>Count.</p>" }, Metadata(), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(2);
        question.Revisions.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("stem")]
    [InlineData("body")]
    [InlineData("gradingSpec")]
    [InlineData("explanation")]
    [InlineData("maxScore")]
    public void Update_EachContentField_CountsAsContentChange(string field)
    {
        var question = _builder.Approved().Build();
        var original = QuestionBuilder.McqContent();
        var content = field switch
        {
            "stem" => original with { Stem = "<p>Changed</p>" },
            "body" => original with { Body = """{"options":[{"id":"a","text":"5"},{"id":"b","text":"4"}]}""" },
            "gradingSpec" => original with { GradingSpec = """{"correctOptionId":"a"}""" },
            "explanation" => original with { Explanation = "<p>Changed</p>" },
            _ => original with { MaxScore = 2 },
        };

        question.Update(QuestionType.Mcq, content, Metadata(), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(2);
    }

    [Fact]
    public void Update_MetadataOnlyOnApproved_KeepsApprovedAndVersion()
    {
        var question = _builder.Approved().Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Hard, _builder.ObjectiveId, ["kinematics"]), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        question.Version.Should().Be(1);
        (question.Difficulty, question.ObjectiveId).Should().Be((QuestionDifficulty.Hard, (Guid?)_builder.ObjectiveId));
        question.Tags.Should().Equal("kinematics");
    }

    [Fact]
    public void Update_MetadataOnly_AddsNoRevision()
    {
        var question = _builder.Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Easy, null, ["si"]), _builder.Lesson, _editor);

        question.Revisions.Should().HaveCount(1);
    }

    [Fact]
    public void Update_EquivalentJsonWithDifferentFormatting_IsNotAContentChange()
    {
        var question = _builder.Approved().Build();
        var body = question.Body;
        var reformatted = QuestionBuilder.McqContent() with
        {
            Body = """{ "options": [ { "text": "3", "id": "a" }, { "text": "4", "id": "b" } ] }""",
            GradingSpec = """{  "correctOptionId" : "b" }""",
        };

        question.Update(QuestionType.Mcq, reformatted, Metadata(), _builder.Lesson, _editor);

        question.Version.Should().Be(1);
        question.Body.Should().BeSameAs(body);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
    }

    [Fact]
    public void Update_NothingChanged_DoesNotStampUpdate()
    {
        var question = _builder.Build();
        var (updatedBy, updationDate) = (question.UpdatedBy, question.UpdationDate);

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent(), Metadata(), _builder.Lesson, _editor);

        question.UpdatedBy.Should().Be(updatedBy);
        question.UpdationDate.Should().Be(updationDate);
    }

    [Fact]
    public void Update_TypeChanged_ThrowsQuestionTypeImmutable()
    {
        var question = _builder.Build();

        var act = () => question.Update(QuestionType.Multi, QuestionBuilder.McqContent(), Metadata(), _builder.Lesson, _editor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionTypeImmutable);
        question.Version.Should().Be(1);
    }

    [Fact]
    public void Update_ObjectiveNotInLesson_ThrowsQuestionObjectiveNotInLesson()
    {
        var question = _builder.Build();

        var act = () => question.Update(QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, Guid.NewGuid(), []), _builder.Lesson, _editor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionObjectiveNotInLesson);
    }

    [Fact]
    public void Update_TwoContentEdits_ReachesVersionThreeWithThreeRevisions()
    {
        var question = _builder.Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>Second</p>" }, Metadata(), _builder.Lesson, _editor);
        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>Third</p>" }, Metadata(), _builder.Lesson, _editor);

        question.Version.Should().Be(3);
        question.Revisions.Select(x => x.Version).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Update_ContentChange_SetsUpdatedByAndUpdationDate()
    {
        var question = _builder.Build();
        var before = question.UpdationDate;

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { MaxScore = 3 }, Metadata(), _builder.Lesson, _editor);

        question.UpdatedBy.Should().Be(_editor);
        question.UpdationDate.Should().BeAfter(before);
    }

    private static QuestionMetadata Metadata() => new(QuestionDifficulty.Medium, null, []);
}
