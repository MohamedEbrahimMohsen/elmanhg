using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Realtime;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;

public sealed class ProcessTeacherThreadSlaHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherThreadSlaEventRepository teacherThreadSlaEventRepository, ITeacherSubjectRepository teacherSubjectRepository, ITeacherThreadNotifier teacherThreadNotifier, ITeacherThreadOutOfAppReminderRepository teacherThreadOutOfAppReminderRepository, IUserRepository userRepository, IEnumerable<IMessageChannel> messageChannels, ElmanhgMetrics metrics, IRuntimeSettings runtimeSettings, TimeProvider timeProvider, ILogger<ProcessTeacherThreadSlaHandler> logger) : IRequestHandler<ProcessTeacherThreadSlaCommand>
{
    public async Task Handle(ProcessTeacherThreadSlaCommand request, CancellationToken cancellationToken)
    {
        var thread = await teacherThreadRepository.FirstOrDefaultAsync(x => x.Id == request.ThreadId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (thread is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var due = thread.DueSlaStages(now);
        if (due.Count == 0)
        {
            return;
        }

        var recorded = await teacherThreadSlaEventRepository.FindAsync(x => x.ThreadId == thread.Id && x.WindowStartedAt == thread.SlaWindowStartedAt, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var missing = due
            .Except(recorded.Select(x => x.Kind))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var kind in missing)
        {
            await teacherThreadSlaEventRepository.AddAsync(TeacherThreadSlaEvent.Record(thread.Id, kind, thread.SlaWindowStartedAt, thread.SlaDueAt, thread.TeacherId, now), cancellationToken).ConfigureAwait(false);
        }

        var values = await runtimeSettings.GetValuesAsync(cancellationToken).ConfigureAwait(false);
        var outOfAppChannels = await OutOfAppTeacherReminder.ClaimAsync(thread, missing, values, teacherThreadOutOfAppReminderRepository, now, cancellationToken).ConfigureAwait(false);
        await teacherThreadSlaEventRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var kind in missing)
        {
            metrics.RecordAskTeacherSlaEvent(kind);
        }

        if (missing.Contains(TeacherThreadSlaEventKind.Breach))
        {
            logger.LogWarning("Ask a Teacher thread {ThreadId} passed its reply deadline {SlaDueAt} without a reply.", thread.Id, thread.SlaDueAt);
        }

        var reminders = missing
            .Where(x => x != TeacherThreadSlaEventKind.Breach)
            .ToList();
        if (reminders.Count == 0)
        {
            return;
        }

        var recipients = await TeacherThreadReminderRecipients.LoadAsync(thread, teacherSubjectRepository, cancellationToken).ConfigureAwait(false);
        if (recipients.Count == 0)
        {
            return;
        }

        await teacherThreadNotifier.NotifyReminderAsync(recipients, thread.Id, reminders.Max(), cancellationToken).ConfigureAwait(false);
        if (outOfAppChannels.Count > 0)
        {
            await OutOfAppTeacherReminder.SendAsync(thread, recipients, outOfAppChannels, userRepository, messageChannels, logger, cancellationToken).ConfigureAwait(false);
        }
    }
}
