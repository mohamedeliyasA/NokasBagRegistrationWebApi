using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.API.Configuration;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public class ServiceBusSender : IServiceBusSender
    {
        private readonly ServiceBusClient _client;
        private readonly string _topicName;

        public ServiceBusSender(IOptions<ServiceBusOptions> options)
        {
            var config = options.Value;
            _client = new ServiceBusClient(config.ConnectionString);
            _topicName = config.TopicName;
        }

        public async Task SendMessageAsync(string messageBody, string subject)
        {
            var sender = _client.CreateSender(_topicName);
            var message = new ServiceBusMessage(messageBody)
            {
                Subject = subject,
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message);
        }
    }
}