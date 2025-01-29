// Copyright 2020 Energinet DataHub A/S
//
// Licensed under the Apache License, Version 2.0 (the "License2");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Scheduler;

public class SchedulerHandler(
    ILogger<SchedulerHandler> logger,
    IClock clock,
    IScheduledOrchestrationInstancesByInstantQuery query,
    IServiceScopeFactory serviceScopeFactory)
{
    private readonly ILogger _logger = logger;
    private readonly IClock _clock = clock;
    private readonly IScheduledOrchestrationInstancesByInstantQuery _query = query;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

    public async Task StartScheduledOrchestrationInstancesAsync()
    {
        var now = _clock.GetCurrentInstant();
        var scheduledOrchestrationInstances = await _query
            .FindAsync(scheduledToRunBefore: now)
            .ConfigureAwait(false);

        foreach (var orchestrationInstance in scheduledOrchestrationInstances)
        {
            try
            {
                // Use a separate scope per command to avoid that data for one command (in the database context)
                // impacts the ability to commit the data for another command.
                using var scope = _serviceScopeFactory.CreateScope();
                var command = scope.ServiceProvider.GetRequiredService<IStartScheduledOrchestrationInstanceCommand>();
                await command
                    .StartScheduledOrchestrationInstanceAsync(orchestrationInstance.Id)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log error if orchestration instance did not start successfully.
                // Does not throw exception since we want to continue processing the next scheduled orchestration instance.
                _logger.LogError(
                    ex,
                    "Failed to start orchestration instance with id = {OrchestrationInstanceId}",
                    orchestrationInstance.Id.Value);
            }
        }
    }
}
