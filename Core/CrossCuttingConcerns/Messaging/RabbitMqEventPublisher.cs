using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;

namespace Core.CrossCuttingConcerns.Messaging
{
    public class RabbitMqEventPublisher : IEventPublisher
    {
        private readonly IConnection _connection;
        private readonly RabbitMqOptions _options;

        public RabbitMqEventPublisher(IConnection connection, IOptions<RabbitMqOptions> options)
        {
            _connection = connection;
            _options = options.Value;
            if (string.IsNullOrWhiteSpace(_options.ExchangeName))
                throw new ArgumentException("RabbitMQ exchange name must be configured.", nameof(options));
        }

        public async Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken cancellationToken = default)
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true,
                autoDelete: false, cancellationToken: cancellationToken);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Type = typeof(TEvent).FullName
            };
            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event));
            await channel.BasicPublishAsync(_options.ExchangeName, routingKey, mandatory: true,
                basicProperties: properties, body: body, cancellationToken: cancellationToken);
        }
    }
}
