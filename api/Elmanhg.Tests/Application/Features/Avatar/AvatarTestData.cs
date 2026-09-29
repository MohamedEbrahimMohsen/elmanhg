using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.SendAvatarMessage;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.ContentRetrieval.SearchLessonContent;
using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.ContentRetrieval;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Infrastructure.RichText;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Avatar;

public static class AvatarTestData
{
    public static void StubSessions(ISessionRepository repository, params Session[] sessions)
    {
        repository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Session, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call => sessions.FirstOrDefault(call.Arg<Expression<Func<Session, bool>>>().Compile()));
        repository.FindAsync(Arg.Any<Expression<Func<Session, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())
            .Returns(call => sessions.Where(call.Arg<Expression<Func<Session, bool>>>().Compile()).ToList());
    }

    public static void StubUsedToday(IAvatarMessageUsageRepository repository, int used)
    {
        repository.CountOnDayAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(used);
    }

    public static LessonContentMatchResult Match(string reference, LessonContentSection section, string? title = null, Guid? questionId = null) => new(Guid.NewGuid(), section, title, 0, questionId, reference, "content " + reference, 0.9);

    public static void StubSearch(ISender sender, params LessonContentMatchResult[] matches)
    {
        sender.Send(Arg.Any<SearchLessonContentQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => new LessonContentSearchResult(call.Arg<SearchLessonContentQuery>().LessonId, DateTimeOffset.UnixEpoch, [.. matches]));
    }

    public sealed class SendHarness
    {
        public static readonly DateTimeOffset Now = ExamSessionBuilder.Now.AddHours(2);

        public SendHarness()
        {
            CurrentUser.UserId.Returns(Builder.StudentId);
            Time.GetUtcNow().Returns(Now);
            Lessons.GetWithObjectivesAsync(Lesson.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Lesson);
            Lessons.GetPublishedSiblingPositionsAsync(Lesson.Id, Arg.Any<CancellationToken>()).Returns([LessonPosition.Of(Lesson)]);
            Units.GetByIdAsync(Builder.Questions.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(Builder.Questions.Unit);
            Subjects.GetByIdAsync(Builder.Questions.Subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(Builder.Questions.Subject);
            SubscriptionRepositoryStub.Stub(Subscriptions);
            StubUsedToday(Usage, 0);
            StubSessions(Sessions);
            StubSearch(Sender);
            Ai.ChatAsync(Arg.Do<AiChatRequest>(x => LastChat = x), Arg.Any<CancellationToken>()).Returns(new AiChatReply("رد", "claude-sonnet-5", "v2", 10, 5, "end_turn", []));
        }

        public ExamSessionBuilder Builder { get; } = new();
        public Lesson Lesson => Builder.Questions.Lesson;
        public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();
        public ILessonRepository Lessons { get; } = Substitute.For<ILessonRepository>();
        public ICurriculumUnitRepository Units { get; } = Substitute.For<ICurriculumUnitRepository>();
        public ISubjectRepository Subjects { get; } = Substitute.For<ISubjectRepository>();
        public IQuestionRepository Questions { get; } = Substitute.For<IQuestionRepository>();
        public ISubscriptionRepository Subscriptions { get; } = Substitute.For<ISubscriptionRepository>();
        public IAvatarMessageUsageRepository Usage { get; } = Substitute.For<IAvatarMessageUsageRepository>();
        public IAiServiceClient Ai { get; } = Substitute.For<IAiServiceClient>();
        public ISender Sender { get; } = Substitute.For<ISender>();
        public TimeProvider Time { get; } = Substitute.For<TimeProvider>();
        public ICurrentUserService CurrentUser { get; } = Substitute.For<ICurrentUserService>();
        public AvatarOptions AvatarOptions { get; } = new();
        public AiChatRequest? LastChat { get; private set; }

        public Task<AvatarReplyResult> SendAsync(SendAvatarMessageCommand command)
        {
            var handler = new SendAvatarMessageHandler(Sessions, Lessons, Units, Subjects, Questions, Subscriptions, Usage, new RichTextExtractor(), Ai, Sender, Options.Create(AvatarOptions), Options.Create(new SubscriptionsOptions()), Options.Create(new ExamsOptions()), Options.Create(new ContentRetrievalOptions()), Time, CurrentUser);
            return handler.Handle(command, TestContext.Current.CancellationToken);
        }

        public SendAvatarMessageCommand LessonCommand(string message = "ما هو قانون أوم؟") => new(AvatarEntryPoint.Lesson, Lesson.Id, null, null, [], message);

        public SendAvatarMessageCommand QuestionCommand(AvatarEntryPoint entryPoint, Session session, Guid questionId) => new(entryPoint, null, session.Id, questionId, [], "لماذا إجابتي خطأ؟");

        public Session Quiz(bool answered)
        {
            var questions = RegisterQuestions(2);
            var session = Session.StartQuiz(Builder.StudentId, Lesson, questions, false);
            if (answered)
            {
                session.RecordAttempt(session.Items[0], SessionBuilder.AnswerA, SessionBuilder.Grade(0m), 1000);
            }

            StubSessions(Sessions, session);
            return session;
        }

        public List<Question> RegisterQuestions(int count)
        {
            var questions = Builder.BuildQuestions(count);
            Questions.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(call => questions.Where(x => call.Arg<IReadOnlyCollection<Guid>>().Contains(x.Id)).SelectMany(x => x.Revisions).ToList());
            Questions.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<bool>())
                .Returns(call => questions.FirstOrDefault(x => x.Id == call.Arg<Guid>()));
            return questions;
        }
    }
}
