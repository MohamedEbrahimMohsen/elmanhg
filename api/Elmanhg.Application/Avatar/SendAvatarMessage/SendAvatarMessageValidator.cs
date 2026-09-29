using Core.Validation.Extensions;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Avatar.SendAvatarMessage;

public sealed class SendAvatarMessageValidator : AbstractValidator<SendAvatarMessageCommand>
{
    public SendAvatarMessageValidator(IOptions<AvatarOptions> avatarOptions)
    {
        var options = avatarOptions.Value;

        RuleFor(x => x.EntryPoint).IsInEnum().WithErrorCode(ErrorCodes.AvatarEntryPointInvalid);
        RuleFor(x => x.LessonId.GetValueOrDefault())
            .ValidateRequired(ErrorCodes.LessonIdRequired)
            .When(x => x.EntryPoint == AvatarEntryPoint.Lesson)
            .OverridePropertyName(nameof(SendAvatarMessageCommand.LessonId));
        RuleFor(x => x.SessionId.GetValueOrDefault())
            .ValidateRequired(ErrorCodes.SessionIdRequired)
            .When(x => x.EntryPoint is AvatarEntryPoint.QuizQuestion or AvatarEntryPoint.ExamReview)
            .OverridePropertyName(nameof(SendAvatarMessageCommand.SessionId));
        RuleFor(x => x.QuestionId.GetValueOrDefault())
            .ValidateRequired(ErrorCodes.QuestionIdRequired)
            .When(x => x.EntryPoint is AvatarEntryPoint.QuizQuestion or AvatarEntryPoint.ExamReview)
            .OverridePropertyName(nameof(SendAvatarMessageCommand.QuestionId));
        RuleFor(x => x.Message)
            .ValidateRequired(ErrorCodes.AvatarMessageRequired)
            .ValidateMaxLength(options.MessageMaxLength, ErrorCodes.AvatarMessageTooLong);
        RuleFor(x => x.History).ValidateListMaxItems(options.MaxHistoryMessages, ErrorCodes.AvatarHistoryTooLong);
        RuleFor(x => x.History).Must(Alternates).WithErrorCode(ErrorCodes.AvatarHistoryInvalid);
        RuleForEach(x => x.History).ChildRules(turn =>
        {
            turn.RuleFor(t => t.Role).IsInEnum().WithErrorCode(ErrorCodes.AvatarHistoryInvalid);
            turn.RuleFor(t => t.Content)
                .ValidateRequired(ErrorCodes.AvatarHistoryInvalid)
                .ValidateMaxLength(options.HistoryTurnMaxLength, ErrorCodes.AvatarHistoryInvalid);
        });
    }

    private static bool Alternates(IList<AvatarTurn>? history)
    {
        if (history is null || history.Count % 2 != 0)
        {
            return false;
        }

        return history
            .Select((turn, index) => turn is not null && turn.Role == (index % 2 == 0 ? AvatarTurnRole.User : AvatarTurnRole.Assistant))
            .All(x => x);
    }
}
