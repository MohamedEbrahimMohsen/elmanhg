using Elmanhg.Application.Teachers.GetTeachers;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Teachers.GetTeachers;

public sealed class GetTeachersHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    [Fact]
    public async Task Handle_Teachers_MapsIdAndDisplayName()
    {
        var first = User.CreateTeacher("Amal", "amal@example.com");
        var second = User.CreateTeacher("Karim", "karim@example.com");
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), true).Returns([first, second]);
        var handler = new GetTeachersHandler(_userRepository);

        var result = await handler.Handle(new GetTeachersQuery(), TestContext.Current.CancellationToken);

        result.Select(x => (x.Id, x.DisplayName)).Should().Equal((first.Id, "Amal"), (second.Id, "Karim"));
    }
}
