using Elmanhg.Domain.Subscriptions;
using System.Linq.Expressions;

namespace Elmanhg.Application.Payments.GetPaymentLog;

public static class GetPaymentLogFilter
{
    public static Expression<Func<Payment, bool>> Build(GetPaymentLogQuery query)
    {
        var status = query.Status;
        var plan = query.Plan;
        var needsReview = query.NeedsReview;
        var studentId = query.StudentId;
        var reference = string.IsNullOrWhiteSpace(query.Reference) ? null : query.Reference.Trim();
        Guid? referenceId = Guid.TryParse(reference, out var id) ? id : null;
        var from = query.From?.ToUniversalTime();
        var to = query.To?.ToUniversalTime();

        return x => (status == null || x.Status == status)
            && (plan == null || x.Plan == plan)
            && (!needsReview || (x.ReviewReason != null && x.ReviewResolvedAt == null))
            && (studentId == null || x.StudentId == studentId)
            && (reference == null || x.Id == referenceId || x.PaymobTransactionId == reference || x.RefundTransactionId == reference || x.ProviderOrderId == reference)
            && (from == null || x.CreationDate >= from)
            && (to == null || x.CreationDate < to);
    }
}
