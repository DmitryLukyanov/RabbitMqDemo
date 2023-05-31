using BackgroundWorker.Utils;
using RabbitMq.Utils;
using RabbitMQ.Client;

namespace BackgroundWorker.HostedServices
{
    public class RabbitMqHostService : IHostedService
    {
        private readonly IHashesRepository _hashesRepository;
        private readonly ILogger<RabbitMqHostService> _logger;
        private readonly SettingsProvider _settingsProvider;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly CancellationToken _cancellationToken;
        private readonly IConnection _connection;
        private readonly List<Task> _tasks;
        private readonly IRabbitMqHelper _rabbitMqHelper;
        private readonly SemaphoreSlim _semaphore;

        public RabbitMqHostService(
            IHashesRepository hashesRepository,
            ILogger<RabbitMqHostService> logger,
            IRabbitMqHelper rabbitMqHelper,
            SettingsProvider settingsProvider)
        {
            _semaphore = new SemaphoreSlim(1, 1);
            _hashesRepository = hashesRepository;
            _logger = logger;
            _rabbitMqHelper = rabbitMqHelper;
            _settingsProvider = settingsProvider;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            _connection = _rabbitMqHelper.CreateConnection(_settingsProvider);
            _tasks = new List<Task>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _hashesRepository.EnsureConfiguredAsync();

            using var effectiveTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);

            const int threadsNumber = 4;
            for (int i = 0; i < threadsNumber; i++)
            {
                _tasks.Add(Action(_connection, i, effectiveTokenSource.Token));
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _connection.Dispose();
            _semaphore.Dispose();
            await Task.WhenAll(_tasks); // TODO: should be quick but add timeout
        }

        private async Task Action(IConnection connection, int index, CancellationToken cancellationToken)
        {
            await Task.Yield();

            _logger.LogInformation($"A queue consumer [{index}] has been started..");

            int processed = 0;
            try
            {
                await using (var concurrentBatchProcesser = new ConcurrentBatchProcesser(_semaphore, _hashesRepository))
                {
                    _rabbitMqHelper.ConsumeListener(
                        connection,
                        _settingsProvider,
                        async (hash) =>
                        {
                            await concurrentBatchProcesser.AddOrSaveAsync(hash);

                            processed++;
                            if (processed % 100 == 0)
                            {
                                _logger.LogInformation($"A queue consumer [{index}] processed {processed} records.");
                            }
                        },
                        prefetchCount: 100,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }

            _logger.LogInformation($"A queue consumer [{index}] has been ended.");
        }

        public class ConcurrentBatchProcesser : IAsyncDisposable
        {
            private readonly IHashesRepository _hashesRepository;
            private readonly List<string> _queue;
            private readonly SemaphoreSlim _semaphore;
            private const int BatchSize = 500;
            private readonly Timer _timer;

            public ConcurrentBatchProcesser(SemaphoreSlim semaphore, IHashesRepository hashesRepository)
            {
                _hashesRepository = hashesRepository;
                _semaphore = semaphore;
                _queue = new();
                _timer = new Timer(async (state) => 
                {
                    try
                    {
                        await AddOrSaveAsyncInternal(null, flush: true);
                    }
                    catch
                    {
                        // ignore for now
                    }
                },
                state: null,
                (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
                (int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            }

            public Task AddOrSaveAsync(string hash) => AddOrSaveAsyncInternal(hash, flush: false);

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await AddOrSaveAsyncInternal(null, flush: true);
                }
                catch
                {
                    // ignore for now
                }

                try
                {
                    await _timer.DisposeAsync();
                }
                catch
                { 
                }
            }

            private async Task AddOrSaveAsyncInternal(string hash, bool flush = false)
            {
                try
                {
                    await _semaphore.WaitAsync();
                    if (!flush && _queue.Count < BatchSize)
                    {
                        _queue.Add(hash);
                    }
                    else
                    {
                        if (!flush)
                        {
                            _queue.Add(hash);
                        }

                        var toProcess = _queue.ToList();
                        _queue.Clear();
                        if (toProcess.Count > 0)
                        {
                            await _hashesRepository.SaveHashesAsync(toProcess);
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
    }
}
