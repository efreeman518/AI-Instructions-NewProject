using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FunctionApp;

public class MaintenanceFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<MaintenanceFunction>();

    [Function("MaintenanceFunction")]
    public void Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("Maintenance function executed at: {Moment}", DateTime.UtcNow);

        if (myTimer.ScheduleStatus is not null)
        {
            _logger.LogInformation("Next maintenance schedule at: {Next}", myTimer.ScheduleStatus.Next);
        }
    }
}
