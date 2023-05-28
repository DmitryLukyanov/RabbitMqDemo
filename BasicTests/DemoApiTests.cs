using RabbitMq.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BasicTests
{
    public class DemoApiTests
    {
        [Fact]
        public void PublishAndConsumeShouldWorkAsExpected()
        {
            var helper = new RabbitMqHelper();
            var settings = new TestRabbitMqSettings();
            using var connection = helper.CreateConnection(settings);
            using var cancellationTokenSource = new CancellationTokenSource();

            const int allRecords = 11;
            int counter = 0;

            var task1 = Task.Run(() => helper.ConsumeListener(
                connection,
                settings,
                (hash) =>
                {
                    Interlocked.Increment(ref counter);
                },
                cancellationTokenSource.Token));

            var task2 = Task.Run(() => helper.ConsumeListener(
                connection,
                settings,
                (hash) =>
                {
                    Interlocked.Increment(ref counter);
                },
                cancellationTokenSource.Token));

            var task3 = Task.Run(() => helper.ConsumeListener(
                connection,
                settings,
                (hash) =>
                {
                    Interlocked.Increment(ref counter);
                },
                cancellationTokenSource.Token));

            helper.GenerateAndPublishBatches(
                connection,
                settings,
                getBatchItem: () =>
                {
                    return new byte[0];
                },
                allRecordsCount: allRecords,
                batchSize: 2);

            Assert.Equal(counter, allRecords);
            Assert.False(task1.IsCompleted);
            Assert.False(task2.IsCompleted);
            Assert.False(task3.IsCompleted);

            cancellationTokenSource.Cancel();

            Assert.Equal(counter, allRecords);
            Assert.True(SpinWait.SpinUntil(() => task1.IsCompleted, TimeSpan.FromSeconds(5)));
            Assert.True(SpinWait.SpinUntil(() => task2.IsCompleted, TimeSpan.FromSeconds(5)));
            Assert.True(SpinWait.SpinUntil(() => task3.IsCompleted, TimeSpan.FromSeconds(5)));

            Assert.True(false);
        }
    }

    public class TestRabbitMqSettings : IRabbitMqSettingsProvider
    {
        public string QueueExchange => "";

        public string QueueHostName => "messageBroker";

        public string QueueName => "dev-queue-test";
    }
}