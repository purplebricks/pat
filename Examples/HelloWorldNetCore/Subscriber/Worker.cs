using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Subscriber
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly Pat.Subscriber.Subscriber _subscriber;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public Worker(ILogger<Worker> logger, Pat.Subscriber.Subscriber subscriber)
        {
            _logger = logger;
            _subscriber = subscriber;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StartAsync");
            await _subscriber.Initialise(new[] { Assembly.GetExecutingAssembly() });
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("StopAsync");
            _cancellationTokenSource.Cancel();
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ExecuteAsync");
            await _subscriber.ListenForMessages(_cancellationTokenSource);
        }
    }
}