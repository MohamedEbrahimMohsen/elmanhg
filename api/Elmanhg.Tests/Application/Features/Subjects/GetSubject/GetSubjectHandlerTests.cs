using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subjects.GetSubject;
using Elmanhg.Application.Units.Shared;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subjects.GetSubject;

public sealed class GetSubjectHandlerTests
{
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly GetSubjectHandler _handler;

    public GetSubjectHandlerTests()
    {
        _handler = new GetSubjectHandler(_subjectRepository, _unitRepository);
    }

    [Fact]
    public async Task Handle_ExistingSubject_ReturnsSubjectWithUnits()
    {
        var subject = Subject.Create("Physics", 2, Guid.NewGuid());
        var first = CurriculumUnit.Create(subject, "Mechanics", 1, Guid.NewGuid());
        var second = CurriculumUnit.Create(subject, "Waves", 2, Guid.NewGuid());
        _subjectRepository.GetByIdAsync(subject.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<bool>()).Returns(subject);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([first, second]);

        var result = await _handler.Handle(new GetSubjectQuery(subject.Id), TestContext.Current.CancellationToken);

        result.Id.Should().Be(subject.Id);
        result.Name.Should().Be("Physics");
        result.Order.Should().Be(2);
        result.Units.Should().Equal(new UnitResult(first.Id, subject.Id, "Mechanics", 1), new UnitResult(second.Id, subject.Id, "Waves", 2));
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetSubjectQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
    }
}
