using System;
using System.Threading.Tasks;
using Contract;
using Microsoft.Extensions.Logging;
using Pat.Subscriber;

namespace Subscriber
{
    public class FooHandler : IHandleEvent<Foo>
    {
        private readonly ILogger _log;

        public FooHandler(ILogger log)
        {
            _log = log;
        }

        public Task HandleAsync(Foo @event)
        {
            _log.LogInformation($"Handling: {@event}");
            if (DateTime.UtcNow.Minute % 3 == 0)
            {
                throw new SampleTransientException();
            }

            if (DateTime.UtcNow.Minute % 5 == 0)
            {
                throw new SamplePermanentException();
            }

            return Task.CompletedTask;
        }
    }
}