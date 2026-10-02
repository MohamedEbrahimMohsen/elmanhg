using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.GetMyAvatarConversations;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Avatar.GetMyAvatarConversations;

public sealed class GetMyAvatarConversationsHandlerTests
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
    private Expression<Func<AvatarConversation, bool>>? _filter;

    public GetMyAvatarConversationsHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(Now);
        AvatarTestData.StubSessions(_sessionRepository);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => new List<Lesson> { _curriculum.Lesson }.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => new List<Subject> { _curriculum.Subject }.Where(call.Arg<Expression<Func<Subject, bool>>>().Compile()).ToList());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle();

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _conversations.DidNotReceive().FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<AvatarConversation, bool>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IQueryable<AvatarConversation>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IOrderedQueryable<AvatarConversation>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_ExamInProgress_ThrowsAvatarExamInProgress()
    {
        AvatarTestData.StubSessions(_sessionRepository, _builder.Build());

        var act = () => Handle();

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.AvatarExamInProgress);
        await _conversations.DidNotReceive().FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<AvatarConversation, bool>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IQueryable<AvatarConversation>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IOrderedQueryable<AvatarConversation>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_Student_ReturnsPageWithNamesAndFirstQuestion()
    {
        var conversation = LessonConversation(_builder.StudentId);
        ArrangePage(new PageData<AvatarConversation> { Items = [conversation], PageNumber = 2, PageSize = 1, TotalItems = 3, TotalPages = 3 });

        var page = await Handle();

        var item = page.Items.Should().ContainSingle().Subject;
        (item.Id, item.EntryPoint, item.SubjectId, item.LessonId).Should().Be((conversation.Id, AvatarEntryPoint.Lesson, (Guid?)_curriculum.Subject.Id, (Guid?)_curriculum.Lesson.Id));
        (item.SubjectName, item.LessonName, item.FirstQuestion, item.MessageCount).Should().Be(("Physics", "Newton's laws", "What is force?", 4));
        (page.PageNumber, page.PageSize, page.TotalItems, page.TotalPages).Should().Be((2, 1, 3, 3));
    }

    [Fact]
    public async Task Handle_Student_FiltersByCaller()
    {
        ArrangePage(new PageData<AvatarConversation> { Items = [], PageNumber = 1, PageSize = 20, TotalItems = 0, TotalPages = 0 });

        await Handle();

        var filter = _filter!.Compile();
        filter(LessonConversation(_builder.StudentId)).Should().BeTrue();
        filter(LessonConversation(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GlobalConversation_ReturnsNullNames()
    {
        var conversation = new AvatarConversationBuilder().ForStudent(_builder.StudentId).WithExchange("Hello?", "Hi").Build();
        ArrangePage(new PageData<AvatarConversation> { Items = [conversation], PageNumber = 1, PageSize = 20, TotalItems = 1, TotalPages = 1 });

        var page = await Handle();

        var item = page.Items.Should().ContainSingle().Subject;
        (item.SubjectName, item.LessonName, item.FirstQuestion).Should().Be(((string?)null, (string?)null, "Hello?"));
    }

    private AvatarConversation LessonConversation(Guid studentId) => new AvatarConversationBuilder()
        .ForStudent(studentId)
        .WithEntryPoint(AvatarEntryPoint.Lesson)
        .WithLesson(_curriculum.Subject.Id, _curriculum.Unit.Id, _curriculum.Lesson.Id)
        .WithExchange("What is force?", "F = m a")
        .WithExchange("And mass?", "kg")
        .Build();

    private void ArrangePage(PageData<AvatarConversation> page)
    {
        _conversations.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Do<Expression<Func<AvatarConversation, bool>>?>(x => _filter = x), Arg.Any<Func<IQueryable<AvatarConversation>, IQueryable<AvatarConversation>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IOrderedQueryable<AvatarConversation>>?>(), true)
            .Returns(page);
    }

    private Task<PageData<StudentAvatarConversationResult>> Handle() => new GetMyAvatarConversationsHandler(_conversations, _subjectRepository, _lessonRepository, _sessionRepository, Options.Create(new ExamsOptions()), _timeProvider, _currentUserService).Handle(new GetMyAvatarConversationsQuery(), TestContext.Current.CancellationToken);
}
