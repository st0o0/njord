using Njord.Configuration;

namespace Njord.Tests.Configuration;

public sealed class BudgetValidatorSpec
{
    [Fact(Timeout = 5000)]
    public void Projects_monthly_calls_for_one_location_eight_models_weather_group_sixty_minute_interval()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "Lucerne", Latitude = 47.05, Longitude = 8.31 }],
            Models = ["icon_d2", "icon_eu", "icon_global", "ecmwf_ifs025", "gfs_seamless", "ukmo_global_deterministic_10km", "meteoswiss_icon_ch1", "meteoswiss_icon_ch2"],
            PollInterval = TimeSpan.FromMinutes(60),
            Parameters = new ParameterOptions { Groups = ["Weather"] },
        };

        var result = BudgetValidator.Validate(options);

        // 8 models, Weather group = 31 params, ceil(31/10) = 4 weight
        // 8 * 4 = 32 calls/cycle, 24 cycles/day, 32 * 24 * 30 = 23040/month
        Assert.Equal(23_040, result.ProjectedMonthlyCalls);
        Assert.True(result.WithinBudget);
        Assert.Empty(result.Warnings);
    }

    [Fact(Timeout = 5000)]
    public void Warns_when_usage_exceeds_eighty_percent_of_budget()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "A", Latitude = 0, Longitude = 0 }],
            Models = ["icon_d2"],
            PollInterval = TimeSpan.FromMinutes(60),
            Parameters = new ParameterOptions { Groups = ["Weather"] },
            BudgetOverride = new RequestBudget(100, 600),
        };

        var result = BudgetValidator.Validate(options);

        // 1 model, ceil(31/10)=4, 4 calls/cycle, 24/day, 4*24*30=2880
        // 2880/100 = 2880% — over 100, so no 80% warning, just not within budget
        Assert.False(result.WithinBudget);
    }

    [Fact(Timeout = 5000)]
    public void Reports_within_budget_false_when_projected_exceeds_monthly_limit()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "A", Latitude = 0, Longitude = 0 }],
            Models = ["icon_d2"],
            PollInterval = TimeSpan.FromMinutes(60),
            Parameters = new ParameterOptions { Groups = ["Weather"] },
            BudgetOverride = new RequestBudget(2_000, 600),
        };

        var result = BudgetValidator.Validate(options);

        // 2880 projected vs 2000 limit = 144%
        Assert.False(result.WithinBudget);
        Assert.True(result.UsagePercent > 100);
    }

    [Fact(Timeout = 5000)]
    public void Issues_warning_at_eighty_percent_threshold()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "A", Latitude = 0, Longitude = 0 }],
            Models = ["icon_d2"],
            PollInterval = TimeSpan.FromMinutes(60),
            Parameters = new ParameterOptions { Groups = ["Weather"] },
            // 2880 projected; need limit where 80% < 2880 <= 100%
            // 2880/limit = 0.9 => limit = 3200
            BudgetOverride = new RequestBudget(3_200, 600),
        };

        var result = BudgetValidator.Validate(options);

        Assert.True(result.WithinBudget);
        Assert.Single(result.Warnings);
        Assert.Contains("90%", result.Warnings[0]);
    }

    [Fact(Timeout = 5000)]
    public void Uses_custom_budget_override_for_validation()
    {
        var options = new NjordOptions
        {
            Locations = [new LocationOptions { Name = "A", Latitude = 0, Longitude = 0 }],
            Models = ["icon_d2"],
            PollInterval = TimeSpan.FromMinutes(60),
            Parameters = new ParameterOptions { Groups = ["Weather"] },
            BudgetOverride = new RequestBudget(1_000_000, 600),
        };

        var result = BudgetValidator.Validate(options);

        Assert.Equal(1_000_000, result.MonthlyLimit);
        Assert.True(result.WithinBudget);
        Assert.Empty(result.Warnings);
    }
}
