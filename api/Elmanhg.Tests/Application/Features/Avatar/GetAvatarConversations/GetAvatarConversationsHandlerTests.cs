using Core.DDD.Models;
using Elmanhg.Application.Avatar.GetAvatarConversations;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Subjects;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Avatar.GetAvatarConversations;

public sealed class GetAvatarConversationsHandlerTests
{
    private readonly IAvatarConversationRepository _conversations = Substitute.For<IAvatarConversationRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly QuestionBuilder _curriculum = new();
    private readonly List<User> _users = [];
    private readonly List<Lesson> _lessons = [];
    private readonly List<Subject> _subjects = [];

    public GetAvatarConversationsHandlerTests()
    {
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => _users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>())
            .Returns(call => _lessons.Where(call.Arg<Expression<Func<Lesson, bool>>>().Compile()).ToList());
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>())
            .Returns(call => _subjects.Where(call.Arg<Expression<Func<Subject, bool>>>().Compile()).ToList());
    }

    [Fact]
    public async Task Handle_Page_MapsStudentSubjectLessonNamesAndFirstQuestion()
    {
        var student = User.CreateStudentWithEmail("Sara Ahmed", "sara@example.com");
        _users.Add(student);
        _lessons.Add(_curriculum.Lesson);
        _subjects.Add(_curriculum.Subject);
        var conversation = LessonConversation(student.Id);
        ArrangePage(new PageData<AvatarConversation> { Items = [conversation], PageNumber = 2, PageSize = 1, TotalItems = 3, TotalPages = 3 });

        var page = await Handle(Query());

        var item = page.Items.Should().ContainSingle().Subject;
        (item.Id, item.StudentId, item.StudentName, item.EntryPoint).Should().Be((conversation.Id, student.Id, "Sara Ahmed", AvatarEntryPoint.Lesson));
        (item.SubjectName, item.LessonName, item.FirstQuestion, item.MessageCount).Should().Be(("Physics", "Newton's laws", "What is force?", 4));
        (page.PageNumber, page.PageSize, page.TotalItems, page.TotalPages).Should().Be((2, 1, 3, 3));
    }

    [Fact]
    public async Task Handle_MissingLookups_ReturnsEmptyStudentNameAndNullNames()
    {
        ArrangePage(new PageData<AvatarConversation> { Items = [LessonConversation(Guid.NewGuid())], PageNumber = 1, PageSize = 20, TotalItems = 1, TotalPages = 1 });

        var page = await Handle(Query());

        var item = page.Items.Should().ContainSingle().Subject;
        (item.StudentName, item.SubjectName, item.LessonName).Should().Be((string.Empty, (string?)null, (string?)null));
    }

    [Fact]
    public async Task Handle_SearchMatchesStudentName_ReturnsThatStudentsConversation()
    {
        var sara = User.CreateStudentWithEmail("Sara Ahmed", "sara@example.com");
        var omar = User.CreateStudentWithEmail("Omar", "omar@example.com");
        _users.AddRange([sara, omar]);
        var saraConversation = new AvatarConversationBuilder().ForStudent(sara.Id).WithExchange("q", "r").Build();
        List<AvatarConversation> stored = [saraConversation, new AvatarConversationBuilder().ForStudent(omar.Id).WithExchange("q", "r").Build()];
        _conversations.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<AvatarConversation, bool>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IQueryable<AvatarConversation>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IOrderedQueryable<AvatarConversation>>?>(), true)
            .Returns(call =>
            {
                var items = stored.Where(call.Arg<Expression<Func<AvatarConversation, bool>>?>()!.Compile()).ToList();
                return new PageData<AvatarConversation> { Items = items, PageNumber = 1, PageSize = 20, TotalItems = items.Count, TotalPages = 1 };
            });

        var page = await Handle(Query() with { Search = "sara" });

        page.Items.Should().ContainSingle().Which.Id.Should().Be(saraConversation.Id);
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
        _conversations.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<AvatarConversation, bool>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IQueryable<AvatarConversation>>?>(), Arg.Any<Func<IQueryable<AvatarConversation>, IOrderedQueryable<AvatarConversation>>?>(), true)
            .Returns(page);
    }

    private static GetAvatarConversationsQuery Query() => new(null, null, null, null);

    private Task<PageData<AdminAvatarConversationResult>> Handle(GetAvatarConversationsQuery query) => new GetAvatarConversationsHandler(_conversations, _userRepository, _lessonRepository, _subjectRepository).Handle(query, TestContext.Current.CancellationToken);
}
