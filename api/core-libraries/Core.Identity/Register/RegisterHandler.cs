//using Core.Identity.Exceptions;
//using MediatR;
//using Microsoft.AspNetCore.Identity;

//namespace Core.Identity.Register;

//public class RegisterHandler<TUser>(UserManager<TUser> userManager) : IRequestHandler<RegisterCommand<TUser>, RegisterResult> where TUser : IdentityUser, new()
//{
//    private readonly UserManager<TUser> _userManager = userManager;

//    public async Task<RegisterResult> Handle(RegisterCommand<TUser> request, CancellationToken cancellationToken)
//    {
//        var result = await _userManager.CreateAsync(request.User, request.Password);

//        //if (!result.Succeeded)
//        //{
//        //    throw new BadRequestOperationException(ErrorCodes.UserCreationFailed, innerException: new Exception(string.Join(", ", result.Errors.Select(x => x.Code))));
//        //}

//        return new RegisterResult(Guid.Parse(request.User.Id));
//    }
//}
