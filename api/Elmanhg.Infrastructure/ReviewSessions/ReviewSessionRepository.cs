using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.ReviewSessions;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.ReviewSessions;

public class ReviewSessionRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<ReviewSession>(context, currentUser, timeProvider), IReviewSessionRepository { }
