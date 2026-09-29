using Elmanhg.Application;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Subscriptions;

public sealed class SubscriptionsOptionsTests
{
    private const string BasePricesMessage = "Subscriptions:BasePrices needs at least one period, each with Months 1-36 and AmountMinor > 0.";

    [Fact]
    public void AddApplication_BasePricesMissing_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Subscriptions:AskTeacherMonthlyPriceMinor"] = "9900" });

        var act = () => provider.GetRequiredService<IOptions<SubscriptionsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().Contain(BasePricesMessage);
    }

    [Fact]
    public void AddApplication_BasePriceNotPositive_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(WithPrices(new Dictionary<string, string?> { ["Subscriptions:BasePrices:Monthly:AmountMinor"] = "0" }));

        var act = () => provider.GetRequiredService<IOptions<SubscriptionsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().Contain(BasePricesMessage);
    }

    [Fact]
    public void AddApplication_AskTeacherPriceMissing_ThrowsOptionsValidationException()
    {
        var settings = WithPrices([]);
        settings.Remove("Subscriptions:AskTeacherMonthlyPriceMinor");
        using var provider = BuildProvider(settings);

        var act = () => provider.GetRequiredService<IOptions<SubscriptionsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddApplication_CurrencyNotIsoCode_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(WithPrices(new Dictionary<string, string?> { ["Subscriptions:Currency"] = "egp" }));

        var act = () => provider.GetRequiredService<IOptions<SubscriptionsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddApplication_ConfiguredPrices_ResolvesWithPrdDefaults()
    {
        using var provider = BuildProvider(WithPrices([]));

        var options = provider.GetRequiredService<IOptions<SubscriptionsOptions>>().Value;

        (options.Currency, options.GracePeriodDays, options.FreeDailyQuizQuestions, options.FreeDailyAvatarMessages, options.FreeOpenLessonsPerUnit, options.BaseDailyAvatarMessages, options.AskTeacherMonthlyQuestions, options.AskTeacherReplySlaHours).Should().Be(("EGP", 3, 10, 5, 1, 50, 20, 24));
        (options.BasePrices[BillingPeriod.Termly].Months, options.BasePrices[BillingPeriod.Termly].AmountMinor).Should().Be((4, 69900L));
    }

    private static Dictionary<string, string?> WithPrices(Dictionary<string, string?> overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Subscriptions:BasePrices:Monthly:Months"] = "1",
            ["Subscriptions:BasePrices:Monthly:AmountMinor"] = "19900",
            ["Subscriptions:BasePrices:Termly:Months"] = "4",
            ["Subscriptions:BasePrices:Termly:AmountMinor"] = "69900",
            ["Subscriptions:BasePrices:Yearly:Months"] = "12",
            ["Subscriptions:BasePrices:Yearly:AmountMinor"] = "179900",
            ["Subscriptions:AskTeacherMonthlyPriceMinor"] = "9900",
        };
        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        return settings;
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
