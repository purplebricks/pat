using System;
using System.Threading.Tasks;
using log4net;
using Microsoft.Azure.ServiceBus;
using Pat.Subscriber;
using Pat.Subscriber.MessageProcessing;

namespace Subscriber
{
    internal class PoisonMessageProcessingBehaviour : IMessageProcessingBehaviour
    {
        private readonly ILog _log;
        private readonly SubscriberConfiguration _config;

        public PoisonMessageProcessingBehaviour(ILog log, SubscriberConfiguration config)
        {
            _log = log;
            _config = config;
        }

        public async Task Invoke(Func<MessageContext, Task> next, MessageContext messageContext)
        {
            var message = messageContext.Message;
            try
            {
                await next(messageContext).ConfigureAwait(false);
            }
            catch (SamplePermanentException ex)
            {
                var messageType = GetMessageType(message);
                var correlationId = GetCollelationId(message);
                await messageContext.MessageReceiver.DeadLetterAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                _log.Warn($"{ex.GetType()}: message deadlettered. `{messageType}` correlation id `{correlationId}` on subscriber `{_config.SubscriberName}`.", ex);

            }
        }

        private static string GetMessageType(Message message)
        {
            return message.UserProperties.ContainsKey("MessageType")
                ? message.UserProperties["MessageType"].ToString()
                : "Unknown Message Type";
        }

        private static string GetCollelationId(Message message)
        {
            return message.UserProperties.ContainsKey("PBCorrelationId")
                ? message.UserProperties["PBCorrelationId"].ToString()
                : "null";
        }
    }
}