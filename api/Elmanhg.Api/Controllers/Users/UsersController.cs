using Core.DDD.Models;
using Elmanhg.Application.Users.GetUsers;
using Elmanhg.Application.Users.InviteUser;
using Elmanhg.Application.Users.ReactivateUser;
using Elmanhg.Application.Users.Shared;
using Elmanhg.Application.Users.SuspendUser;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet(Name = "GetUsers")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType<PageData<UserSummaryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUsers([FromQuery] string? search, [FromQuery] UserStatus? status, [FromQuery] UserRole role = UserRole.Student, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetUsersQuery(role, search, status, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpPost("invitations", Name = "InviteUser")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType<InviteUserResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> InviteUser([FromBody] InviteUserCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId:guid}/suspend", Name = "SuspendUser")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SuspendUser([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        await mediator.Send(new SuspendUserCommand(userId), cancellationToken);
        return Ok();
    }

    [HttpPost("{userId:guid}/reactivate", Name = "ReactivateUser")]
    [Authorize(Policy = DefaultCodes.UsersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReactivateUser([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReactivateUserCommand(userId), cancellationToken);
        return Ok();
    }
}
