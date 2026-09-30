using Core.DDD.Models;
using Elmanhg.Application.GradeReviews.GetEssayGradeReview;
using Elmanhg.Application.GradeReviews.GetGradeReviewQueue;
using Elmanhg.Application.GradeReviews.GetGradeReviewSubjects;
using Elmanhg.Application.GradeReviews.GetMathStepGradeReview;
using Elmanhg.Application.GradeReviews.ReviewEssayGrade;
using Elmanhg.Application.GradeReviews.ReviewMathStepGrade;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Elmanhg.Api.Controllers.GradeReviews;

[ApiController]
[Route("api")]
[Authorize]
public class GradeReviewsController(IMediator mediator) : ControllerBase
{
    [HttpGet("grade-reviews/subjects", Name = "GetGradeReviewSubjects")]
    [Authorize(Policy = DefaultCodes.AiGradesOverride)]
    [ProducesResponseType<List<GradeReviewSubjectResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetGradeReviewSubjects(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGradeReviewSubjectsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subjects/{subjectId:guid}/grade-reviews", Name = "GetGradeReviewQueue")]
    [Authorize(Policy = DefaultCodes.AiGradesOverride)]
    [ProducesResponseType<PageData<GradeReviewItemResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetGradeReviewQueue([FromRoute] Guid subjectId, [FromQuery] GradeReviewKind kind = GradeReviewKind.Essay, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetGradeReviewQueueQuery(subjectId, kind, pageNumber, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subjects/{subjectId:guid}/grade-reviews/essays/{essayGradeId:guid}", Name = "GetEssayGradeReview")]
    [Authorize(Policy = DefaultCodes.AiGradesOverride)]
    [ProducesResponseType<GradeReviewDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetEssayGradeReview([FromRoute] Guid subjectId, [FromRoute] Guid essayGradeId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetEssayGradeReviewQuery(subjectId, essayGradeId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("subjects/{subjectId:guid}/grade-reviews/essays/{essayGradeId:guid}", Name = "ReviewEssayGrade")]
    [Authorize(Policy = DefaultCodes.AiGradesOverride)]
    [ProducesResponseType<GradeReviewDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReviewEssayGrade([FromRoute] Guid subjectId, [FromRoute] Guid essayGradeId, [FromBody] ReviewGradeRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewEssayGradeCommand(subjectId, essayGradeId, request.Decision, request.Score, request.Comment), cancellationToken);
        return Ok(result);
    }

    [HttpGet("subjects/{subjectId:guid}/grade-reviews/math-steps/{mathStepGradeId:guid}", Name = "GetMathStepGradeReview")]
    [Authorize(Policy = DefaultCodes.AiGradesOverride)]
    [ProducesResponseType<GradeReviewDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMathStepGradeReview([FromRoute] Guid subjectId, [FromRoute] Guid mathStepGradeId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMathStepGradeReviewQuery(subjectId, mathStepGradeId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("subjects/{subjectId:guid}/grade-reviews/math-steps/{mathStepGradeId:guid}", Name = "ReviewMathStepGrade")]
    [Authorize(Policy = DefaultCodes.AiGradesOverride)]
    [ProducesResponseType<GradeReviewDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReviewMathStepGrade([FromRoute] Guid subjectId, [FromRoute] Guid mathStepGradeId, [FromBody] ReviewGradeRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ReviewMathStepGradeCommand(subjectId, mathStepGradeId, request.Decision, request.Score, request.Comment), cancellationToken);
        return Ok(result);
    }
}
