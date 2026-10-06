using Core.Errors;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Errors;

public sealed class BaseExceptionTests
{
    [Fact]
    public void ErrorCodeOf_CoreExceptionWithCode_ReturnsCode()
    {
        var errorCode = BaseException.ErrorCodeOf(new NotFoundCoreException("X_NOT_FOUND"));

        errorCode.Should().Be("X_NOT_FOUND");
    }

    [Fact]
    public void ErrorCodeOf_BaseExceptionWithoutCode_ReturnsTypeName()
    {
        var withoutCode = BaseException.ErrorCodeOf(new BaseException());
        var emptyCode = BaseException.ErrorCodeOf(new BaseException(""));

        (withoutCode, emptyCode).Should().Be(("BaseException", "BaseException"));
    }

    [Fact]
    public void ErrorCodeOf_OtherException_ReturnsTypeName()
    {
        var errorCode = BaseException.ErrorCodeOf(new IOException());

        errorCode.Should().Be("IOException");
    }
}
