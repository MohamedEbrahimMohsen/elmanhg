using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Identity;

public partial class User
{
    public string? TermsVersion { get; private set; }
    public DateTimeOffset? TermsAcceptedAt { get; private set; }

    public void AcceptTerms(string termsVersion, DateTimeOffset acceptedAt)
    {
        if (!TermsVersions.IsKnown(termsVersion))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.TermsVersionUnknown);
        }

        TermsVersion = termsVersion;
        TermsAcceptedAt = acceptedAt;
        UpdationDate = acceptedAt;
    }
}
