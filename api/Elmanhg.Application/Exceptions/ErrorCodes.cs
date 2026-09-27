namespace Elmanhg.Application.Exceptions;

public static class ErrorCodes
{
    // AUTH
    public const string UserNotAuthenticated = "USER_NOT_AUTHENTICATED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string UserInvalidLogin = "USER_INVALID_LOGIN";
    public const string UserLockedOut = "USER_LOCKED_OUT";
    public const string UserSuspended = "USER_SUSPENDED";
    public const string UserCreationFailed = "USER_CREATION_FAILED";
    public const string PhoneNumberAlreadyRegistered = "PHONE_NUMBER_ALREADY_REGISTERED";
    public const string PhoneNumberNotRegistered = "PHONE_NUMBER_NOT_REGISTERED";
    public const string EmailAlreadyRegistered = "EMAIL_ALREADY_REGISTERED";
    public const string OtpInvalid = "OTP_INVALID";

    // VALIDATION
    public const string OtpVerificationIdInvalidFormat = "OTP_VERIFICATION_ID_INVALID_FORMAT";
    public const string RefreshTokenIsRequired = "REFRESH_TOKEN_IS_REQUIRED";
    public const string DisplayNameRequired = "DISPLAY_NAME_REQUIRED";
    public const string DisplayNameTooLong = "DISPLAY_NAME_TOO_LONG";
    public const string EmailRequired = "EMAIL_REQUIRED";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string EmailTooLong = "EMAIL_TOO_LONG";
    public const string PasswordIsRequired = "PASSWORD_IS_REQUIRED";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string PasswordMustContainDigit = "PASSWORD_MUST_CONTAIN_DIGIT";

    // PLATFORM
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string AdminSeedFailed = "ADMIN_SEED_FAILED";
}
