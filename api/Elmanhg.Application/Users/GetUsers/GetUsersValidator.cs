using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Users.GetUsers;

public sealed class GetUsersValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersValidator(IOptions<UsersOptions> usersOptions)
    {
        var options = usersOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.ListMaxPageSize, ErrorCodes.UserListPageNumberInvalid, ErrorCodes.UserListPageSizeInvalid);
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.UserListRoleInvalid);
        RuleFor(x => x.Status)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.UserListStatusInvalid);
        RuleFor(x => x.Search).ValidateMaxLength(options.SearchMaxLength, ErrorCodes.UserListSearchTooLong);
    }
}
