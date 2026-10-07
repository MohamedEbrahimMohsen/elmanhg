using Core.Settings;
using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;

public static class OutOfAppTeacherReminder
{
    public static async Task<IReadOnlyList<MessageChannel>> ClaimAsync(TeacherThread thread, IReadOnlyCollection<TeacherThreadSlaEventKind> newlyDueStages, RuntimeSettingValues values, ITeacherThreadOutOfAppReminderRepository repository, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!values.Get(OutOfAppReminderRuntimeSettings.Enabled))
        {
            return [];
        }

        var stage = Enum.Parse<TeacherThreadSlaEventKind>(values.Get(OutOfAppReminderRuntimeSettings.Stage));
        if (!newlyDueStages.Contains(stage))
        {
            return [];
        }

        if (await repository.IsRecordedAsync(thread.Id, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        await repository.AddAsync(TeacherThreadOutOfAppReminder.Record(thread.Id, stage, thread.SlaDueAt, now), cancellationToken).ConfigureAwait(false);
        return ChannelsOf(values.Get(OutOfAppReminderRuntimeSettings.Channels));
    }

    public static async Task SendAsync(TeacherThread thread, IReadOnlyCollection<Guid> recipientIds, IReadOnlyList<MessageChannel> channels, IUserRepository userRepository, IEnumerable<IMessageChannel> messageChannels, ILogger logger, CancellationToken cancellationToken)
    {
        var users = await userRepository.FindAsync(x => recipientIds.Contains(x.Id) && x.Status == UserStatus.Active && x.PasswordHash != null, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var context = thread.ReadContext();
        foreach (var user in users.OrderBy(x => x.Id))
        {
            foreach (var channel in channels)
            {
                var address = AddressOf(user, channel);
                if (string.IsNullOrWhiteSpace(address))
                {
                    logger.LogInformation("Out-of-app reminder for thread {ThreadId} skipped {Channel} for user {UserId}: no contact on file.", thread.Id, channel, user.Id);
                    continue;
                }

                var delivered = await messageChannels.Single(x => x.Channel == channel).SendAsync(new TeacherThreadReminderMessage(user.Id, address, user.DisplayName, thread.Id, context.SubjectName, context.LessonName, thread.SlaDueAt), cancellationToken).ConfigureAwait(false);
                logger.Log(delivered ? LogLevel.Information : LogLevel.Warning, "Out-of-app reminder for thread {ThreadId} over {Channel} to user {UserId}: {Outcome}.", thread.Id, channel, user.Id, delivered ? "Delivered" : "Failed");
            }
        }
    }

    private static IReadOnlyList<MessageChannel> ChannelsOf(string value) => value == OutOfAppReminderRuntimeSettings.BothChannels ? [MessageChannel.WhatsApp, MessageChannel.Email] : [Enum.Parse<MessageChannel>(value)];

    private static string? AddressOf(User user, MessageChannel channel) => channel switch
    {
        MessageChannel.WhatsApp => user.PhoneNumber,
        MessageChannel.Email => user.Email,
        _ => null,
    };
}
