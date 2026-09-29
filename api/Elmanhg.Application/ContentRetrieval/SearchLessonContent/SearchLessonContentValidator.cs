using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ContentRetrieval.SearchLessonContent;

public sealed class SearchLessonContentValidator : AbstractValidator<SearchLessonContentQuery>
{
    public SearchLessonContentValidator(IOptions<ContentRetrievalOptions> contentRetrievalOptions)
    {
        var options = contentRetrievalOptions.Value;

        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.Query)
            .ValidateRequired(ErrorCodes.ContentSearchQueryRequired)
            .ValidateMaxLength(options.QueryMaxLength, ErrorCodes.ContentSearchQueryTooLong);
        RuleFor(x => x.Top.GetValueOrDefault())
            .ValidateRange(1, options.MaxTopK, ErrorCodes.ContentSearchTopInvalid)
            .When(x => x.Top.HasValue)
            .OverridePropertyName(nameof(SearchLessonContentQuery.Top));
    }
}
