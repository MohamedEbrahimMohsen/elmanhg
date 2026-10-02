using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Avatar.SendAvatarMessage;

public sealed partial class SendAvatarMessageHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionRepository questionRepository, ISubscriptionRepository subscriptionRepository, IAvatarMessageUsageRepository avatarMessageUsageRepository, IAvatarConversationRepository avatarConversationRepository, IRichTextExtractor richTextExtractor, IAiServiceClient aiServiceClient, ISender sender, IOptions<AvatarOptions> avatarOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, IOptions<ExamsOptions> examsOptions, IOptions<ContentRetrievalOptions> contentRetrievalOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<SendAvatarMessageCommand, AvatarReplyResult>
{
    public async Task<AvatarReplyResult> Handle(SendAvatarMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var userId = currentUserService.UserId.Value;
        var now = timeProvider.GetUtcNow();
        await AvatarGate.EnsureNoExamInProgressAsync(userId, sessionRepository, examsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var used = await AvatarGate.EnsureMessageAvailableAsync(entitlement, userId, avatarMessageUsageRepository, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var conversation = await LoadConversationAsync(request, userId, cancellationToken).ConfigureAwait(false);
        var context = await LoadContextAsync(request, userId, entitlement, cancellationToken).ConfigureAwait(false);
        var history = HistoryOf(conversation);
        var sources = context.Matches
            .Select(AvatarSourceFactory.Create)
            .ToList();
        var reply = await aiServiceClient.ChatAsync(new AiChatRequest(context.Bundle, history, request.Message.Trim(), sources), cancellationToken).ConfigureAwait(false);
        var citations = AvatarCitationMapper.Map(reply.Citations, context.Matches, context.LessonId);
        var repliedAt = timeProvider.GetUtcNow();

        var isNew = conversation is null;
        conversation ??= StartConversation(request, userId, context.Bundle, now);
        conversation.RecordExchange(request.Message, new AvatarAssistantReply(reply.Reply, reply.Model, reply.PromptVersion, reply.InputTokens, reply.OutputTokens, reply.CostUsd, reply.StopReason, history.Count, AvatarMessageJson.WriteContext(context.Bundle, sources), AvatarMessageJson.WriteCitations(citations)), now, repliedAt);
        if (isNew)
        {
            await avatarConversationRepository.AddAsync(conversation, cancellationToken).ConfigureAwait(false);
        }

        await avatarMessageUsageRepository.AddAsync(AvatarMessageUsage.Record(userId, request.EntryPoint, now), cancellationToken).ConfigureAwait(false);
        await avatarMessageUsageRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var limit = entitlement.DailyAvatarMessageLimit;
        return new AvatarReplyResult(conversation.Id, reply.Reply, citations, limit, used + 1, Math.Max(0, limit - used - 1), reply.Model, reply.PromptVersion);
    }
}
