namespace Core.Messaging.Email;

public sealed record EmailMessage(string From, string To, string Subject, string Html, string Text);
