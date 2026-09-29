using Core.Errors;
using Elmanhg.Application.Avatar.GetAvatarConversation;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Avatar.GetAvatarConversation;

public sealed class GetAvatarConversationHandlerTests
{
    private readonly IAvatarConversationRepository _conversations = Substitute.For<IAvatarConversationRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly QuestionBuilder _curriculum = new();
    private readonly User _student = User.CreateStudentWithEmail("Sara Ahmed", "sara@example.com");

    public GetAvatarConversationHandlerTests()
    {
        _userRepository.GetByIdAsync(_student.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<bool>()).Returns(_student);
        _subjectRepository.GetByIdAsync(_curriculum.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_curriculum.Subject);
        _unitRepository.GetByIdAsync(_curriculum.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_curriculum.Unit);
        _lessonRepository.GetByIdAsync(_curriculum.Lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_curriculum.Lesson);
    }

    [Fact]
    public async Task Handle_Unknown_ThrowsAvatarConversationNotFound()
    {
        Arrange();

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationNotFound);
    }

    [Fact]
    public async Task Handle_Found_ReturnsMessagesInOrderWithReplyMetadataContextAndCitations()
    {
        var citation = new AvatarCitationResult("explanation-1", LessonContentSection.Explanation, "قانون أوم", _curriculum.Lesson.Id, null);
        var conversation = Conversation(AvatarConversationBuilder.Reply("r1") with { Citations = AvatarMessageJson.WriteCitations([citation]) }, AvatarConversationBuilder.Reply("r2"));
        Arrange(conversation);

        var result = await Handle(conversation.Id);

        result.Messages.Select(x => x.Position).Should().Equal(0, 1, 2, 3);
        (result.StudentName, result.SubjectName, result.UnitName, result.LessonName).Should().Be(("Sara Ahmed", "Physics", "Mechanics", "Newton's laws"));
        var assistant = result.Messages[1];
        (assistant.Role, assistant.Model, assistant.CostUsd).Should().Be((AvatarMessageRole.Assistant, "claude-sonnet-5", (decimal?)0.0021m));
        assistant.Context!.Bundle.EntryPoint.Should().Be(AiChatEntryPoint.Global);
        assistant.Citations.Should().Equal(citation);
        var student = result.Messages[0];
        (student.Role, student.Context).Should().Be((AvatarMessageRole.Student, (AvatarMessageContext?)null));
        student.Citations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Found_SumsTokensAndCost()
    {
        var conversation = Conversation(AvatarConversationBuilder.Reply("r1", 0.001m), AvatarConversationBuilder.Reply("r2", 0.0025m));
        Arrange(conversation);

        var result = await Handle(conversation.Id);

        (result.TotalInputTokens, result.TotalOutputTokens, result.TotalCostUsd).Should().Be((200, 40, (decimal?)0.0035m));
    }

    private AvatarConversation Conversation(params AvatarAssistantReply[] replies)
    {
        var conversation = new AvatarConversationBuilder()
            .ForStudent(_student.Id)
            .WithEntryPoint(AvatarEntryPoint.Lesson)
            .WithLesson(_curriculum.Subject.Id, _curriculum.Unit.Id, _curriculum.Lesson.Id)
            .Build();
        var at = AvatarConversationBuilder.DefaultStartedAt;
        foreach (var reply in replies)
        {
            at = at.AddMinutes(1);
            conversation.RecordExchange("q", reply, at, at);
        }

        return conversation;
    }

    private void Arrange(params AvatarConversation[] conversations)
    {
        _conversations.FirstOrDefaultAsync(Arg.Any<Expression<Func<AvatarConversation, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<AvatarConversation>, IQueryable<AvatarConversation>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IOrderedQueryable<AvatarConversation>>?>(), Arg.Any<bool>())
            .Returns(call => conversations.FirstOrDefault(call.Arg<Expression<Func<AvatarConversation, bool>>>().Compile()));
    }

    private Task<AdminAvatarConversationDetailResult> Handle(Guid conversationId) => new GetAvatarConversationHandler(_conversations, _userRepository, _subjectRepository, _unitRepository, _lessonRepository).Handle(new GetAvatarConversationQuery(conversationId), TestContext.Current.CancellationToken);
}
