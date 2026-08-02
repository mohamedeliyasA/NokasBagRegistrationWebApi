using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.API.Configuration;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public class ServiceBusSender : IServiceBusSender, IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly Azure.Messaging.ServiceBus.ServiceBusSender _sender;

        public ServiceBusSender(IOptions<ServiceBusOptions> options)
        {
            var config = options.Value;
            _client = new ServiceBusClient(config.ConnectionString);
            _sender = _client.CreateSender(config.TopicName);
        }

        public async Task SendMessageAsync(string messageBody, string subject, string? messageId = null, CancellationToken cancellationToken = default)
        {
            var message = new ServiceBusMessage(messageBody)
            {
                Subject = subject,
                ContentType = "application/json"
            };

            if (!string.IsNullOrEmpty(messageId))
            {
                message.MessageId = messageId;
            }

            await _sender.SendMessageAsync(message, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync();
            await _client.DisposeAsync();
        }
    }
}