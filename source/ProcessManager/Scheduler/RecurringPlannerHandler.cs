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

using Energinet.DataHub.ProcessManagement.Core.Application.Scheduling;
using Microsoft.EntityFrameworkCore.SqlServer.NodaTime.Extensions;
using Microsoft.Extensions.Logging;
using NCrontab;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Scheduler;

public class RecurringPlannerHandler(
    ILogger<RecurringPlannerHandler> logger,
    IClock clock,
    IRecurringOrchestrationQueries query)
{
    private readonly ILogger _logger = logger;
    private readonly IClock _clock = clock;
    private readonly IRecurringOrchestrationQueries _query = query;

    public async Task PerformRecurringPlanningAsync()
    {
        // - We should use NodaTime to determine "current" time, and ensure we use timezone "Europe/Copenhagen".
        // - We should use a configured "actor id" for scheduling new orchestration instances.
        //
        // Psuedo code:
        // 1. Find OrchestrationDescriptions that are recurring
        // 2. For each recurring OrchestrationDescription
        // 2.a. Determine "occurences" within the next 24 hours (five minutes from now)
        // 2.b. Determine already scheduled instances within the next 24 hours (five minutes from now)
        // 2.c. Compare the two lists, and schedule a new orchestration instance for any not appearing in "occurences"

        var now = _clock.GetCurrentInstant();
        var runAtOrLater = now.PlusMinutes(5);
        var runAtOrEarlier = runAtOrLater.PlusHours(24);

        var orchestrationDescriptions = await _query
            .SearchRecurringOrchestrationDescriptionsAsync()
            .ConfigureAwait(false);

        foreach (var orchestrationDescription in orchestrationDescriptions)
        {
            try
            {
                var cronSchedule = CrontabSchedule.Parse(orchestrationDescription.RecurringCronExpression);
                var scheduleAtOccurrences = cronSchedule.GetNextOccurrences(
                    runAtOrLater.ToDateTimeUtc(),
                    runAtOrEarlier.ToDateTimeUtc());

                var scheduledInstances = await _query
                    .SearchScheduledOrchestrationInstancesAsync(
                        orchestrationDescription.UniqueName,
                        runAtOrLater,
                        runAtOrEarlier)
                    .ConfigureAwait(false);

                var missingOccurrences = scheduleAtOccurrences
                    .Where(datetime => !scheduledInstances.Any(instance => instance.Lifecycle.ScheduledToRunAt!.Value.ToDateTimeUtc() == datetime))
                    .ToList();

                foreach (var occurrence in missingOccurrences)
                {
                    // TODO: Schedule orchestration instance
                }
            }
            catch (Exception ex)
            {
                // Log error if we could not schedule successfully.
                // Does not throw exception since we want to continue processing the next orchestration description.
                _logger.LogError(
                    ex,
                    "Failed to schedule orchestration instances for orchestration description with id = {OrchestrationDescriptionId}",
                    orchestrationDescription.Id.Value);
            }
        }
    }
}
