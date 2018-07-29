using System;
using System.Threading.Tasks;
using Contract;
using Microsoft.Extensions.Logging;
using Pat.Subscriber;

namespace Subscriber
{
    public class FooHandler : IHandleEvent<Foo>
    {
        private readonly ILogger _logger;

        public FooHandler(ILogger logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(Foo @event)
        {
            _logger.LogInformation($"Handling: {@event}");
            if (DateTime.UtcNow.Minute % 5 == 0)
            {
                throw new ExternalProviderOfflineException();
            }

            return Task.CompletedTask;
        }
    }
}