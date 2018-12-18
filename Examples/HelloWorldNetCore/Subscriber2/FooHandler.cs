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

        public FooHandler(ILogger<FooHandler> log)
        {
            _log = log;
        }
        
        public async Task HandleAsync(Foo @event)
        {
            Console.WriteLine(DateTime.Now);
            _log.LogInformation("Handling: {event}", @event);
            throw new Exception("Blow up lol");
            await Task.CompletedTask;
        }
    }
}