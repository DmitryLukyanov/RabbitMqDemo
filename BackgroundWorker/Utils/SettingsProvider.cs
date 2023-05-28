using RabbitMq.Utils;

namespace BackgroundWorker.Utils
{
    public class SettingsProvider : IRabbitMqSettingsProvider
    {
        private readonly IConfiguration _configuration;

        public SettingsProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string QueueExchange => ""; // use default one
        public string QueueHostName => _configuration.GetValue<string>(nameof(QueueHostName));
        public string QueueName => _configuration.GetValue<string>(nameof(QueueName));
    }
}
