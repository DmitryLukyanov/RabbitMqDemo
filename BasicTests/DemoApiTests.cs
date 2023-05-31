using RabbitMq.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BasicTests
{
    public class DemoApiTests
    {
        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(10)]
        public void PublishAndConsumeShouldWorkAsExpected(int numberOfThreads)
        {
            var helper = new RabbitMqHelper();
            var settings = new TestRabbitMqSettings();
            using var connection = helper.CreateConnection(settings);
            using var cancellationTokenSource = new CancellationTokenSource();

            const int allRecords = 29;

            var tasksInfo = new List<TaskInfo>();
            for (int i = 0; i < numberOfThreads; i++)
            {
                var taskInfo = new TaskInfo();
                taskInfo.Task = Task.Run(() => helper.ConsumeListener(
                    connection,
                    settings,
                    (hash) => taskInfo.Increment(),
                    prefetchCount: 3,
                    cancellationTokenSource.Token));
                tasksInfo.Add(taskInfo);
            }

            helper.GenerateAndPublishBatches(
                connection,
                settings,
                getBatchItem: () =>
                {
                    return new byte[0];
                },
                allRecordsCount: allRecords,
                batchSize: 2);

            var tasks = tasksInfo.Select(c => c.Task).ToList();
            var counters = tasksInfo.Select(c => c.Counter);
            Assert.True(SpinWait.SpinUntil(() => counters.Sum() == allRecords, TimeSpan.FromSeconds(15)));
            Assert.Equal(counters.Sum(), allRecords);
            foreach (var ti in tasksInfo)
            {
                Assert.False(ti.Task.IsCompleted);
            }

            cancellationTokenSource.Cancel();

            counters = tasksInfo.Select(c => c.Counter).ToList();
            Assert.Equal(counters.Sum(), allRecords);
            foreach (var ti in tasksInfo)
            {
                Assert.True(SpinWait.SpinUntil(() => ti.Task.IsCompleted, TimeSpan.FromSeconds(5)));
            }
        }
    }

    public class TestRabbitMqSettings : IRabbitMqSettingsProvider
    {
        public string QueueExchange => "";

        public string QueueHostName => "localhost";

        public string QueueName => "dev-queue-test";
    }

    public class TaskInfo
    {
        private int _counter;

        public Task Task { get; set; }
        public int Counter => _counter;
        public void Increment() => Interlocked.Increment(ref _counter);
    }
}