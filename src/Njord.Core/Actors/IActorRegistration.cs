using Akka.Hosting;

namespace Njord.Core.Actors;

public interface IActorRegistration
{
    void Configure(AkkaConfigurationBuilder builder, IServiceProvider provider);
}
