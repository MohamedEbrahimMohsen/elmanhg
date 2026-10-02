using Elmanhg.Application.Shared.Messaging;
using System.Collections.Concurrent;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class MessageOutbox
{
    private readonly ConcurrentQueue<(MessageChannel Channel, OutboundMessage Message)> _sent = new();

    public void Record(MessageChannel channel, OutboundMessage message)
    {
        _sent.Enqueue((channel, message));
    }

    public List<(MessageChannel Channel, TeacherThreadReminderMessage Message)> RemindersFor(Guid threadId)
    {
        return _sent
            .Where(x => x.Message is TeacherThreadReminderMessage reminder && reminder.ThreadId == threadId)
            .Select(x => (x.Channel, (TeacherThreadReminderMessage)x.Message))
            .ToList();
    }
}
