using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public static class OptionsRegistrationExtensions
{
    public static OptionsBuilder<T> AddNjordOptions<T>(this IServiceCollection services)
        where T : class, IConfigSection
    {
        if (typeof(IHasArrayDefaults).IsAssignableFrom(typeof(T)))
            services.PostConfigure<T>(o => ((IHasArrayDefaults)o).ApplyDefaults());

        return services.AddOptions<T>().BindConfiguration(T.SectionName).ValidateOnStart();
    }

    public static OptionsBuilder<T> AddNjordOptions<T, TValidator>(this IServiceCollection services)
        where T : class, IConfigSection
        where TValidator : class, IValidateOptions<T>
    {
        services.AddSingleton<IValidateOptions<T>, TValidator>();
        return services.AddNjordOptions<T>();
    }
}
