namespace Elmanhg.Infrastructure.OtpDelivery;

public static class PhoneNumberFormatter
{
    private const char LocalTrunkPrefix = '0';

    public static string ToInternational(string phoneNumber, string countryCallingCode) => phoneNumber.StartsWith(LocalTrunkPrefix) ? countryCallingCode + phoneNumber[1..] : phoneNumber;
}
