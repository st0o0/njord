using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Njord.Core.Configuration;

public static class OptionsRegistrationExtensions
{
    // Binds T to its SectionName, lets a configured list replace the list default, registers the validator
    // named "<T>Validator" in the namespace of T, and validates when the host starts.
    public static OptionsBuilder<T> AddNjordOptions<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class =>
        services.AddNjordOptions<T>(configuration, ConventionalValidator<T>());

    public static OptionsBuilder<T> AddNjordOptions<T, TValidator>(this IServiceCollection services, IConfiguration configuration)
        where T : class
        where TValidator : class, IValidateOptions<T> =>
        services.AddNjordOptions<T>(configuration, typeof(TValidator));

    private static OptionsBuilder<T> AddNjordOptions<T>(
        this IServiceCollection services, IConfiguration configuration, Type validator)
        where T : class
    {
        services.AddSingleton(typeof(IValidateOptions<T>), validator);

        return services
            .AddOptions<T>()
            .Bind(configuration.GetSection(ConfigSections.PathOf<T>()))
            .ReplaceCollectionDefaults()
            .ValidateOnStart();
    }

    private static Type ConventionalValidator<T>() where T : class =>
        typeof(T).Assembly.GetType($"{typeof(T).Namespace}.{typeof(T).Name}Validator")
            is { IsClass: true, IsAbstract: false } type && typeof(IValidateOptions<T>).IsAssignableFrom(type)
            ? type
            : throw new InvalidOperationException(
                $"No validator '{typeof(T).Name}Validator' implementing IValidateOptions<{typeof(T).Name}> "
                + $"was found in namespace '{typeof(T).Namespace}'.");
}
