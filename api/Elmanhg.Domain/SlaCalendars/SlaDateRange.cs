namespace Elmanhg.Domain.SlaCalendars;

public sealed record SlaDateRange(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly day) => day >= Start && day <= End;
}
