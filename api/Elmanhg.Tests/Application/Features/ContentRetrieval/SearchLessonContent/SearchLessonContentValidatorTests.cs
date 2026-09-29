using Elmanhg.Application.ContentRetrieval.SearchLessonContent;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.SearchLessonContent;

public sealed class SearchLessonContentValidatorTests
{
    private const int MaxTopK = 20;
    private const int QueryMaxLength = 50;

    private readonly SearchLessonContentValidator _validator = new(Options.Create(new ContentRetrievalOptions { MaxTopK = MaxTopK, QueryMaxLength = QueryMaxLength }));

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var withoutTop = _validator.Validate(new SearchLessonContentQuery(Guid.NewGuid(), "Ohm's law", null));
        var atMaxTop = _validator.Validate(new SearchLessonContentQuery(Guid.NewGuid(), new string('q', QueryMaxLength), MaxTopK, false));

        withoutTop.IsValid.Should().BeTrue();
        atMaxTop.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new SearchLessonContentQuery(Guid.Empty, "q", null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_BlankQuery_FailsContentSearchQueryRequired()
    {
        var result = _validator.Validate(new SearchLessonContentQuery(Guid.NewGuid(), "   ", null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ContentSearchQueryRequired);
    }

    [Fact]
    public void Validate_QueryOverMax_FailsContentSearchQueryTooLong()
    {
        var result = _validator.Validate(new SearchLessonContentQuery(Guid.NewGuid(), new string('q', QueryMaxLength + 1), null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ContentSearchQueryTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(MaxTopK + 1)]
    public void Validate_TopOutOfRange_FailsContentSearchTopInvalid(int top)
    {
        var result = _validator.Validate(new SearchLessonContentQuery(Guid.NewGuid(), "q", top));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ContentSearchTopInvalid);
    }
}
