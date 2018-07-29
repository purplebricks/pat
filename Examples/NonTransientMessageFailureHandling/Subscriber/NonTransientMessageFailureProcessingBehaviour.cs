using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pat.Subscriber;
using Pat.Subscriber.MessageProcessing;

namespace Subscriber
{
    internal class NonTransientMessageFailureProcessingBehaviour : DefaultMessageProcessingBehaviour
    {
        private readonly ILogger _log;
        private readonly SubscriberConfiguration _config;

        public NonTransientMessageFailureProcessingBehaviour(ILogger log, SubscriberConfiguration config) : base(log, config)
        {
            _log = log;
            _config = config;
        }

        protected override async Task HandleException(Exception ex, MessageContext messageContext)
        {
            if (ex is SamplePermanentException)
            {
                var message = messageContext.Message;
                var messageType = GetMessageType(message);
                var correlationId = GetCorrelationId(message);
                await messageContext.MessageReceiver.DeadLetterAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                _log.LogWarning($"{ex.GetType()}: message deadlettered. `{messageType}` correlation id `{correlationId}` on subscriber `{_config.SubscriberName}`.", ex);
            }
            else
            {
                await base.HandleException(ex, messageContext);
            }
        }
    }
}