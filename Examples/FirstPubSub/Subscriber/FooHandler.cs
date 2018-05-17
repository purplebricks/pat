﻿using System.Threading.Tasks;
using Contract;
using log4net;
using Pat.Subscriber;

namespace Subscriber
{
    public class FooHandler : IHandleEvent<Foo>
    {
        private readonly ILog _log;

        public FooHandler(ILog log)
        {
            _log = log;
        }

        public Task HandleAsync(Foo @event)
        {
            _log.Info($"Handling: {@event}");
            return Task.CompletedTask;
        }
    }
}