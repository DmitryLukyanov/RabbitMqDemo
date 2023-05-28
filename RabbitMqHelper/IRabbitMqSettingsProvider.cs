
namespace RabbitMq.Utils
{
    public interface IRabbitMqSettingsProvider
    {
        string QueueExchange { get; }
        string QueueHostName { get; }
        string QueueName { get; }
    }
}
