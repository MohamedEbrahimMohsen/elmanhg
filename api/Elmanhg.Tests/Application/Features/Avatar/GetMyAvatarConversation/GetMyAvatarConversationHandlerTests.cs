using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.GetMyAvatarConversation;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Avatar.GetMyAvatarConversation;

public sealed class GetMyAvatarConversationHandlerTests
{
    private static readonly DateTimeOffset Now = ExamSessionBuilder.Now.AddMinutes(5);
    private readonly IAvatarConversationRepository _conversations = Substitute.For<IAvatarConversationRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ExamSessionBuilder _builder = new();
    private readonly QuestionBuilder _curriculum = new();

    public GetMyAvatarConversationHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(Now);
        AvatarTestData.StubSessions(_sessionRepository);
        AvatarTestData.StubConversations(_conversations);
        _subjectRepository.GetByIdAsync(_curriculum.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(_curriculum.Subject);
        _lessonRepository.GetByIdAsync(_curriculum.Lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_curriculum.Lesson);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_ExamInProgress_ThrowsAvatarExamInProgress()
    {
        var conversation = Conversation(_builder.StudentId, AvatarConversationBuilder.Reply("r1"));
        AvatarTestData.StubConversations(_conversations, conversation);
        AvatarTestData.StubSessions(_sessionRepository, _builder.Build());

        var act = () => Handle(conversation.Id);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarExamInProgress);
    }

    [Fact]
    public async Task Handle_Own_ReturnsMessagesInOrderWithCitationsAndNames()
    {
        var citation = new AvatarCitationResult("explanation-1", LessonContentSection.Explanation, "قانون أوم", _curriculum.Lesson.Id, null);
        var conversation = Conversation(_builder.StudentId, AvatarConversationBuilder.Reply("r1") with { Citations = AvatarMessageJson.WriteCitations([citation]) }, AvatarConversationBuilder.Reply("r2"));
        AvatarTestData.StubConversations(_conversations, conversation);

        var result = await Handle(conversation.Id);

        result.Messages.Select(x => x.Position).Should().Equal(0, 1, 2, 3);
        result.Messages.Select(x => x.Role).Should().Equal(AvatarMessageRole.Student, AvatarMessageRole.Assistant, AvatarMessageRole.Student, AvatarMessageRole.Assistant);
        result.Messages.Select(x => x.Text).Should().Equal("q1", "r1", "q2", "r2");
        result.Messages[1].Citations.Should().Equal(citation);
        result.Messages[0].Citations.Should().BeEmpty();
        (result.Id, result.EntryPoint, result.SubjectName, result.LessonName, result.MessageCount).Should().Be((conversation.Id, AvatarEntryPoint.Lesson, "Physics", "Newton's laws", 4));
    }

    [Fact]
    public async Task Handle_OtherStudentsConversation_ThrowsNotFound()
    {
        var conversation = Conversation(Guid.NewGuid(), AvatarConversationBuilder.Reply("r1"));
        AvatarTestData.StubConversations(_conversations, conversation);

        var act = () => Handle(conversation.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationNotFound);
    }

    [Fact]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        AvatarTestData.StubConversations(_conversations, Conversation(_builder.StudentId, AvatarConversationBuilder.Reply("r1")));

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarConversationNotFound);
    }

    private AvatarConversation Conversation(Guid studentId, params AvatarAssistantReply[] replies)
    {
        var conversation = new AvatarConversationBuilder()
            .ForStudent(studentId)
            .WithEntryPoint(AvatarEntryPoint.Lesson)
            .WithLesson(_curriculum.Subject.Id, _curriculum.Unit.Id, _curriculum.Lesson.Id)
            .Build();
        var at = AvatarConversationBuilder.DefaultStartedAt;
        for (var index = 0; index < replies.Length; index++)
        {
            at = at.AddMinutes(1);
            conversation.RecordExchange($"q{index + 1}", replies[index], at, at);
        }

        return conversation;
    }

    private Task<StudentAvatarConversationDetailResult> Handle(Guid conversationId) => new GetMyAvatarConversationHandler(_conversations, _subjectRepository, _lessonRepository, _sessionRepository, Options.Create(new ExamsOptions()), _timeProvider, _currentUserService).Handle(new GetMyAvatarConversationQuery(conversationId), TestContext.Current.CancellationToken);
}
