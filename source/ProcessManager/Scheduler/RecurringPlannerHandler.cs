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

using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Microsoft.Extensions.Logging;
using NCrontab;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Scheduler;

public class RecurringPlannerHandler(
    ILogger<RecurringPlannerHandler> logger,
    DateTimeZone dateTimeZone,
    IClock clock,
    IRecurringOrchestrationQueries query,
    IStartOrchestrationInstanceCommands manager)
{
    internal static readonly ActorIdentity DatahubAdministratorActorId =
        new(new ActorId(Guid.Parse("00000000-0000-0000-0000-000000000001")));

    private readonly ILogger _logger = logger;
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;
    private readonly IClock _clock = clock;
    private readonly IRecurringOrchestrationQueries _query = query;
    private readonly IStartOrchestrationInstanceCommands _manager = manager;

    public async Task PerformRecurringPlanningAsync()
    {
        var zonedDateTimeNow = new ZonedDateTime(_clock.GetCurrentInstant(), _dateTimeZone);
        var runAtOrLater = zonedDateTimeNow.PlusMinutes(5);
        var runAtOrEarlier = runAtOrLater.PlusHours(24);

        var orchestrationDescriptions = await _query
            .SearchRecurringOrchestrationDescriptionsAsync()
            .ConfigureAwait(false);

        foreach (var orchestrationDescription in orchestrationDescriptions)
        {
            try
            {
                var cronSchedule = CrontabSchedule.Parse(orchestrationDescription.RecurringCronExpression);
                // Values must NOT be converted to UTC because the cron expression specified by developers
                // are expected to be in local danish time.
                var scheduleAtInTimeZone = cronSchedule.GetNextOccurrences(
                    runAtOrLater.ToDateTimeUnspecified(),
                    runAtOrEarlier.ToDateTimeUnspecified());

                if (scheduleAtInTimeZone.Any())
                {
                    // Values must be converted to UTC
                    var scheduleAtAsInstant = scheduleAtInTimeZone
                        .Select(value =>
                        {
                            var localTime = new LocalTime(value.Hour, value.Minute);
                            var localDate = new LocalDate(value.Year, value.Month, value.Day);
                            var localDateTime = localDate.At(localTime);
                            var dateTimeInZone = localDateTime.InZoneLeniently(_dateTimeZone);

                            return dateTimeInZone.ToInstant();
                        });

                    // In the database 'RunAt' is an instant (UTC), so we must compare using UTC
                    var scheduledInstances = await _query
                        .SearchScheduledOrchestrationInstancesAsync(
                            orchestrationDescription.UniqueName,
                            runAtOrLater.ToInstant(),
                            runAtOrEarlier.ToInstant())
                        .ConfigureAwait(false);

                    var missingOccurrences = scheduleAtAsInstant
                        .Where(x => !scheduledInstances.Any(instance => instance.Lifecycle.ScheduledToRunAt!.Value == x))
                        .ToList();

                    foreach (var occurrence in missingOccurrences)
                    {
                        var orchestrationInstance = await _manager
                            .ScheduleNewOrchestrationInstanceAsync(
                                DatahubAdministratorActorId,
                                orchestrationDescription.UniqueName,
                                runAt: occurrence)
                            .ConfigureAwait(false);
                    }
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
