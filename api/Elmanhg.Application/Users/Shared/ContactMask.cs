namespace Elmanhg.Application.Users.Shared;

public static class ContactMask
{
    // Privacy rule: operators recognise a number by its prefix and last digits.
    private const int VisiblePhoneCharacters = 3;
    private const char MaskCharacter = '*';
    private const string EmailLocalMask = "***";

    public static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        if (phone.Length <= VisiblePhoneCharacters * 2)
        {
            return new string(MaskCharacter, phone.Length);
        }

        return phone[..VisiblePhoneCharacters] + new string(MaskCharacter, phone.Length - (VisiblePhoneCharacters * 2)) + phone[^VisiblePhoneCharacters..];
    }

    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? EmailLocalMask : email[0] + EmailLocalMask + email[at..];
    }
}
