using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Users.GetUsers;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Users.GetUsers;

public sealed class GetUsersValidatorTests
{
    private readonly GetUsersValidator _validator = new(Options.Create(new UsersOptions { ListMaxPageSize = 100, SearchMaxLength = 256 }));

    [Fact]
    public void Validate_Defaults_Passes()
    {
        var result = _validator.Validate(new GetUsersQuery(UserRole.Student, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageNumberZero_FailsWithPageNumberInvalid()
    {
        var result = _validator.Validate(new GetUsersQuery(UserRole.Student, null, null, PageNumber: 0));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserListPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeAboveMax_FailsWithPageSizeInvalid()
    {
        var result = _validator.Validate(new GetUsersQuery(UserRole.Student, null, null, PageSize: 101));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserListPageSizeInvalid);
    }

    [Fact]
    public void Validate_UndefinedRole_FailsWithRoleInvalid()
    {
        var result = _validator.Validate(new GetUsersQuery((UserRole)9, null, null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserListRoleInvalid);
    }

    [Fact]
    public void Validate_UndefinedStatus_FailsWithStatusInvalid()
    {
        var result = _validator.Validate(new GetUsersQuery(UserRole.Student, null, (UserStatus)9));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserListStatusInvalid);
    }

    [Fact]
    public void Validate_SearchTooLong_FailsWithSearchTooLong()
    {
        var result = _validator.Validate(new GetUsersQuery(UserRole.Student, new string('a', 257), null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserListSearchTooLong);
    }
}
