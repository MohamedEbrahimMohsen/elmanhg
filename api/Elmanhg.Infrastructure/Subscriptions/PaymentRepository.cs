using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Data.Context;

namespace Elmanhg.Infrastructure.Subscriptions;

public class PaymentRepository(AppDbContext context) : Repository<Payment>(context), IPaymentRepository { }
