using Elmanhg.Domain.Mastery;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Mastery;

public static class QuestionMasteryRepositoryStub
{
    public static void StubFind(IQuestionMasteryRepository repository, params QuestionMastery[] rows)
    {
        repository.FirstOrDefaultAsync(Arg.Any<Expression<Func<QuestionMastery, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionMastery>, IQueryable<QuestionMastery>>?>(), Arg.Any<Func<IQueryable<QuestionMastery>, IOrderedQueryable<QuestionMastery>>?>(), Arg.Any<bool>())
            .Returns(call => rows.FirstOrDefault(call.Arg<Expression<Func<QuestionMastery, bool>>>().Compile()));
    }
}
