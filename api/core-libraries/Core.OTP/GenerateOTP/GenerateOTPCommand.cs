using MediatR;

namespace Core.OTP.GenerateOTP;

public sealed record GenerateOTPCommand(string? PhoneNumber, string? Email = null) : IRequest<GenerateOTPResult>;
