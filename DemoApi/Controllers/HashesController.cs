using DemoApi.Utils;
using Microsoft.AspNetCore.Mvc;
using RabbitMq.Utils;
using RabbitMQ.Client;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace DemoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HashesController : ControllerBase
    {
        private readonly IConnection _connection;
        private readonly IHashesRepository _hashesRepository;
        private readonly ILogger<HashesController> _logger;
        private readonly SettingsProvider _settingsProvider;
        private readonly IRabbitMqHelper _rabbitMqHelper;

        public HashesController(
            IConnection connection,
            IHashesRepository hashesRepository, 
            ILogger<HashesController> logger, 
            IRabbitMqHelper rabbitMqHelper,
            SettingsProvider settingsProvider)
        {
            _connection = connection;
            _hashesRepository = hashesRepository;
            _logger = logger;
            _rabbitMqHelper = rabbitMqHelper;
            _settingsProvider = settingsProvider;
        }

        [HttpGet("stored/grouped")]
        public ActionResult<IEnumerable<HashGroupedInfo>> GetGrouped()
        {
            var records = _hashesRepository.GetGroupedStoredHashes();
            return Ok(records.Select(r => new HashGroupedInfo() { Date = r.Date, Count = r.Count}));
        }

        [HttpGet("stored")]
        public ActionResult<IEnumerable<HashInfo>> Get([FromQuery(Name = "skip")] int? skip = null, [FromQuery(Name = "take")] int? take = null)
        {
            var records = _hashesRepository.GetStoredHashes(skip, take);
            return Ok(records.Select(r => new HashInfo() { Date = r.Date, Hash = r.Sha }));
        }

        [HttpGet("stored/count")]
        public ActionResult<int> GetCount()
        {
            var records = _hashesRepository.GetStoredHashes();
            return Ok(records.Count());
        }

        [HttpDelete("stored")]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task Remove()
        {
            await _hashesRepository.ClearAllStoredHashes();
        }

        [HttpPost("generatedShaRequests")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task Post()
        {
            var fullCount = _settingsProvider.NumberOfHashes;
            
            List<Task> tasks = new();
            const int threadNumber = 4;
            const int batchSize = 1000;
            var threadChinkLength = fullCount / threadNumber;
            for (int i = 0; i < threadNumber; i++)
            {
                tasks.Add(Action(_connection, batchSize, threadChinkLength));
            }
            var remaining = fullCount - threadChinkLength * threadNumber;
            if (remaining > 0)
            {
                tasks.Add(Action(_connection, batchSize, remaining));
            }

            await Task.WhenAll(tasks);

            async Task Action(IConnection connection, int batchSize, int needToProcess)
            {
                await Task.Yield();

                try
                {
                    _rabbitMqHelper.GenerateAndPublishBatches(
                        connection, 
                        _settingsProvider,
                        () => 
                        {
                            var sha = GenerateHash();
                            return Encoding.UTF8.GetBytes(sha);
                        },
                        allRecordsCount: needToProcess,
                        batchSize);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"A batch generation with batchSize: {batchSize} and expected records: {needToProcess} has failed. {e.Message}");
                    throw;
                }
            }
        }

        private string GenerateHash()
        {
            using var sha1 = SHA1.Create();
            var guid = Guid.NewGuid();
            return Convert.ToHexString(sha1.ComputeHash(guid.ToByteArray()));
        }

        public class HashGroupedInfo
        {
            public DateTime Date { get; set; }
            public int Count { get; set; }
        }

        public class HashInfo
        {
            public DateTime Date { get; set; }
            public string Hash { get; set; }
        }
    }
}