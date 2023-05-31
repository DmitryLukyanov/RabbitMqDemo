using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;

namespace RabbitMq.Utils
{
    public interface IRabbitMqHelper
    {
        IConnection CreateConnection(IRabbitMqSettingsProvider settingsProvider);
        void ConsumeListener(
            IConnection connection, 
            IRabbitMqSettingsProvider settingsProvider, 
            Action<string> receivedCallback, 
            int prefetchCount,
            CancellationToken cancellationToken);
        void GenerateAndPublishBatches(
            IConnection connection,
            IRabbitMqSettingsProvider settingsProvider,
            Func<byte[]> getBatchItem,
            int allRecordsCount,
            int batchSize);
    }

    public class RabbitMqHelper : IRabbitMqHelper
    {
        public IConnection CreateConnection(IRabbitMqSettingsProvider settingsProvider)
        {
            var factory = new ConnectionFactory() { HostName = settingsProvider.QueueHostName, AutomaticRecoveryEnabled = true };

            var timeout = TimeSpan.FromSeconds(20);
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                try
                {
                    return factory.CreateConnection();
                }
                catch
                {
                    // TODO: should be a better way to handle a case when the queue is not ready
                    Thread.Sleep(100);
                }
            }
            while (stopwatch.Elapsed < timeout);

            throw new TimeoutException($"RabbitMQ hasn't been launched during {timeout}.");
        }

        public void ConsumeListener(
            IConnection connection, 
            IRabbitMqSettingsProvider settingsProvider, 
            Action<string> receivedCallback, 
            int prefetchCount,
            CancellationToken cancellationToken)
        {
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: settingsProvider.QueueName, exclusive: false, durable: true, autoDelete: false, arguments: null);
                channel.BasicQos(prefetchSize: 0, prefetchCount: (ushort)prefetchCount, global: false);

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (obj, e) =>
                {
                    var body = e.Body;
                    var hash = Encoding.UTF8.GetString(body.ToArray());

                    (receivedCallback ?? throw new ArgumentNullException("Consumer caller must provide callback"))(hash);
                };

                channel.BasicConsume(settingsProvider.QueueName, autoAck: true, consumer);
                SpinWait.SpinUntil(() => cancellationToken.IsCancellationRequested);
            }
        }

        public void GenerateAndPublishBatches(
            IConnection connection,
            IRabbitMqSettingsProvider settingsProvider,
            Func<byte[]> getBatchItem,
            int allRecordsCount,
            int batchSize)
        {
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(
                    queue: settingsProvider.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                while (allRecordsCount > 0)
                {
                    var batch = channel.CreateBasicPublishBatch();
                    var effectiveBatchSize = Math.Min(batchSize, allRecordsCount);
                    for (int i = 0; i < effectiveBatchSize; i++)
                    {
                        var bodyBytes = getBatchItem();
#pragma warning disable CS0618 // Type or member is obsolete
                        batch.Add(
                            exchange: settingsProvider.QueueExchange,
                            routingKey: settingsProvider.QueueName,
                            mandatory: false,
                            properties: null,
                            bodyBytes);
#pragma warning restore CS0618 // Type or member is obsolete
                    }

                    batch.Publish();
                    allRecordsCount -= batchSize;
                }
            }
        }
    }
}