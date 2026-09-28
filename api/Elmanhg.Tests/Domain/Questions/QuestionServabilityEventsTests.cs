using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionServabilityEventsTests
{
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _editor = Guid.NewGuid();

    [Fact]
    public void Approve_Pending_RaisesQuestionApproved()
    {
        var question = _builder.Build();

        question.Approve(Assignment());

        question.GetDomainEvents().Should().Equal(new QuestionApproved(question.Id, question.LessonId));
    }

    [Fact]
    public void Reject_Pending_RaisesQuestionRejected()
    {
        var question = _builder.Build();

        question.Reject(Assignment(), "Wrong unit");

        question.GetDomainEvents().Should().Equal(new QuestionRejected(question.Id, question.LessonId));
    }

    [Fact]
    public void Update_ContentOnApproved_RaisesQuestionReturnedToPending()
    {
        var question = _builder.Approved().Build();
        question.ClearDomainEvents();

        question.Update(QuestionType.Mcq, EditedContent(), Metadata(QuestionDifficulty.Medium), _builder.Lesson, _editor);

        question.GetDomainEvents().Should().Equal(new QuestionReturnedToPending(question.Id, question.LessonId));
    }

    [Fact]
    public void Update_ContentOnPending_RaisesNoEvent()
    {
        var question = _builder.Build();

        question.Update(QuestionType.Mcq, EditedContent(), Metadata(QuestionDifficulty.Medium), _builder.Lesson, _editor);

        question.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Update_MetadataOnlyOnApproved_RaisesNoEvent()
    {
        var question = _builder.Approved().Build();
        question.ClearDomainEvents();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent(), Metadata(QuestionDifficulty.Hard), _builder.Lesson, _editor);

        question.GetDomainEvents().Should().BeEmpty();
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
    }

    [Fact]
    public void Resubmit_Rejected_RaisesNoEvent()
    {
        var question = _builder.Rejected("Wrong unit").Build();
        question.ClearDomainEvents();

        question.Resubmit(QuestionType.Mcq, EditedContent(), Metadata(QuestionDifficulty.Medium), _builder.Lesson, _editor);

        question.GetDomainEvents().Should().BeEmpty();
    }

    private TeacherSubject Assignment()
    {
        return TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid());
    }

    private static QuestionContent EditedContent()
    {
        return QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" };
    }

    private static QuestionMetadata Metadata(QuestionDifficulty difficulty)
    {
        return new QuestionMetadata(difficulty, null, []);
    }
}
