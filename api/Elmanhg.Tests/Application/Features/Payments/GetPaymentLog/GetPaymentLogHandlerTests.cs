using Core.DDD.Models;
using Elmanhg.Application.Payments.GetPaymentLog;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Payments.GetPaymentLog;

public sealed class GetPaymentLogHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly List<User> _users = [];

    public GetPaymentLogHandlerTests()
    {
        _userRepository.FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => _users.Where(call.Arg<Expression<Func<User, bool>>>().Compile()).ToList());
    }

    [Fact]
    public async Task Handle_Page_ReturnsItemsWithStudentNamesAndPaging()
    {
        var emailStudent = User.CreateStudentWithEmail("Mona Ali", "mona@example.com");
        var phoneStudent = User.CreateStudentWithPhone("Omar Said", "+201000000001");
        _users.AddRange([emailStudent, phoneStudent]);
        var first = PaymentFor(emailStudent.Id);
        var second = PaymentFor(phoneStudent.Id);
        ArrangePage(new PageData<Payment> { Items = [first, second], PageNumber = 2, PageSize = 2, TotalItems = 5, TotalPages = 3 });

        var page = await Handle();

        page.Items.Select(x => (x.Id, x.StudentName, x.StudentContact)).Should().Equal((first.Id, "Mona Ali", "mona@example.com"), (second.Id, "Omar Said", "+201000000001"));
        (page.PageNumber, page.PageSize, page.TotalItems, page.TotalPages).Should().Be((2, 2, 5, 3));
    }

    [Fact]
    public async Task Handle_StudentMissing_ReturnsEmptyStudentName()
    {
        var payment = PaymentFor(Guid.NewGuid());
        ArrangePage(new PageData<Payment> { Items = [payment], PageNumber = 1, PageSize = 20, TotalItems = 1, TotalPages = 1 });

        var page = await Handle();

        var item = page.Items.Should().ContainSingle().Subject;
        (item.StudentName, item.StudentContact, item.Amount).Should().Be((string.Empty, (string?)null, new Money(19900, "EGP")));
    }

    private void ArrangePage(PageData<Payment> page)
    {
        _paymentRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Payment, bool>>?>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), true)
            .Returns(page);
    }

    private Task<PageData<Elmanhg.Application.Payments.Shared.AdminPaymentResult>> Handle() => new GetPaymentLogHandler(_paymentRepository, _userRepository).Handle(new GetPaymentLogQuery(null, null, false, null, null, null, null), TestContext.Current.CancellationToken);

    private static Payment PaymentFor(Guid studentId) => Payment.Create(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
}
