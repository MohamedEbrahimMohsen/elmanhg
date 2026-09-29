using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Avatar;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Avatar;

public class AvatarConversationRepository(AppDbContext context) : Repository<AvatarConversation>(context), IAvatarConversationRepository { }
