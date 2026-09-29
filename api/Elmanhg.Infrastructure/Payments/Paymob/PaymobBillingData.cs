using Elmanhg.Application.Shared.Payments;
using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed record PaymobBillingData([property: JsonPropertyName("first_name")] string FirstName, [property: JsonPropertyName("last_name")] string LastName, [property: JsonPropertyName("email")] string Email, [property: JsonPropertyName("phone_number")] string PhoneNumber, [property: JsonPropertyName("apartment")] string Apartment, [property: JsonPropertyName("floor")] string Floor, [property: JsonPropertyName("street")] string Street, [property: JsonPropertyName("building")] string Building, [property: JsonPropertyName("city")] string City, [property: JsonPropertyName("country")] string Country, [property: JsonPropertyName("state")] string State)
{
    // Paymob rejects blank billing fields; "NA" is its documented placeholder.
    private const string NotAvailable = "NA";

    public static PaymobBillingData From(PaymentCustomer customer, string country)
    {
        var parts = customer.DisplayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new(
            FirstName: parts.Length > 0 ? parts[0] : NotAvailable,
            LastName: parts.Length > 1 ? parts[1] : NotAvailable,
            Email: string.IsNullOrWhiteSpace(customer.Email) ? NotAvailable : customer.Email,
            PhoneNumber: string.IsNullOrWhiteSpace(customer.PhoneNumber) ? NotAvailable : customer.PhoneNumber,
            Apartment: NotAvailable,
            Floor: NotAvailable,
            Street: NotAvailable,
            Building: NotAvailable,
            City: NotAvailable,
            Country: country,
            State: NotAvailable);
    }
}
