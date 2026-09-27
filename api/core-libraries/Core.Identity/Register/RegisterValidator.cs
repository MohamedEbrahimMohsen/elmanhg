//using Core.Identity.Exceptions;
//using Core.Validation.Extensions;
//using FluentValidation;
//using Microsoft.AspNetCore.Identity;

//namespace Core.Identity.Register;

//public class RegisterValidator : AbstractValidator<RegisterCommand<IdentityUser>>
//{
//    public RegisterValidator()
//    {
//        RuleFor(x => x.User)
//            .NotNull()
//            .WithMessage(ErrorCodes.UserIsRequired);

//        RuleFor(x => x.Password)
//            .ValidateRequired(ErrorCodes.PasswordIsRequired);
//    }
//}
