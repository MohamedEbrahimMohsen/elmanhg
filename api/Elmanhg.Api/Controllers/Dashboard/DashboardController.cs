using Elmanhg.Application.Dashboard.GetAskTeacherMetrics;
using Elmanhg.Application.Dashboard.GetContentMetrics;
using Elmanhg.Application.Dashboard.GetFunnelMetrics;
using Elmanhg.Application.Dashboard.GetPaymentMetrics;
using Elmanhg.Application.Dashboard.GetSolveRateMetrics;
using Elmanhg.Application.Dashboard.GetStudentMetrics;
using Elmanhg.Application.Dashboard.GetSubscriberMetrics;
using Elmanhg.Application.Dashboard.GetSuccessRateMetrics;
using Elmanhg.Application.Dashboard.GetValidationMetrics;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("students", Name = "GetDashboardStudents")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<StudentMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetStudents([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStudentMetricsQuery(from, to), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subscribers", Name = "GetDashboardSubscribers")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<SubscriberMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSubscribers([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSubscriberMetricsQuery(from, to), cancellationToken);
        return Ok(result);
    }

    [HttpGet("content", Name = "GetDashboardContent")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<ContentMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetContent([FromQuery] Guid? subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetContentMetricsQuery(subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("solve-rate", Name = "GetDashboardSolveRate")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<SolveRateMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSolveRate([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSolveRateMetricsQuery(from, to, subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("success-rate", Name = "GetDashboardSuccessRate")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<SuccessRateMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSuccessRate([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSuccessRateMetricsQuery(from, to, subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("validation", Name = "GetDashboardValidation")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<ValidationMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetValidation([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetValidationMetricsQuery(from, to, subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("ask-teacher", Name = "GetDashboardAskTeacher")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<AskTeacherMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAskTeacher([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? subjectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAskTeacherMetricsQuery(from, to, subjectId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments", Name = "GetDashboardPayments")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<PaymentMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPayments([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPaymentMetricsQuery(from, to), cancellationToken);
        return Ok(result);
    }

    [HttpGet("funnel", Name = "GetDashboardFunnel")]
    [Authorize(Policy = DefaultCodes.DashboardsView)]
    [ProducesResponseType<FunnelMetricsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetFunnel([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetFunnelMetricsQuery(from, to), cancellationToken);
        return Ok(result);
    }
}
