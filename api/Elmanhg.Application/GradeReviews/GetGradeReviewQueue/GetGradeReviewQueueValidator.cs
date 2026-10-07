using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.GradeReviews.GetGradeReviewQueue;

public sealed class GetGradeReviewQueueValidator : AbstractValidator<GetGradeReviewQueueQuery>
{
    public GetGradeReviewQueueValidator(IOptions<GradeReviewOptions> gradeReviewOptions)
    {
        var options = gradeReviewOptions.Value;

        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.GradeReviewKindInvalid);
        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.QueueMaxPageSize, ErrorCodes.GradeReviewPageNumberInvalid, ErrorCodes.GradeReviewPageSizeInvalid);
    }
}
