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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
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
    internal static readonly UserIdentity RecurringJobIdentity = DataHubSystemAdministratorUser.UserIdentity;

    private readonly ILogger _logger = logger;
    private readonly DateTimeZone _dateTimeZone = dateTimeZone;
    private readonly IClock _clock = clock;
    private readonly IRecurringOrchestrationQueries _query = query;
    private readonly IStartOrchestrationInstanceCommands _manager = manager;

    public async Task PerformRecurringPlanningAsync()
    {
        var nowInTimeZone = new ZonedDateTime(_clock.GetCurrentInstant(), _dateTimeZone);
        var runAtOrLaterInTimeZone = nowInTimeZone.PlusMinutes(5);
        var runAtOrEarlierInTimeZone = runAtOrLaterInTimeZone.PlusHours(24);

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
                    runAtOrLaterInTimeZone.ToDateTimeUnspecified(),
                    runAtOrEarlierInTimeZone.ToDateTimeUnspecified());

                if (scheduleAtInTimeZone.Any())
                {
                    var scheduleAtAsInstants = ConvertToInstants(scheduleAtInTimeZone);

                    // In the database 'RunAt' is an instant (UTC), so we must compare using UTC
                    var scheduledInstances = await _query
                        .SearchScheduledOrchestrationInstancesAsync(
                            orchestrationDescription.UniqueName,
                            runAtOrLaterInTimeZone.ToInstant(),
                            runAtOrEarlierInTimeZone.ToInstant())
                        .ConfigureAwait(false);

                    var missingOccurrences = scheduleAtAsInstants
                        .Where(scheduleAt => !scheduledInstances.Any(instance => instance.Lifecycle.ScheduledToRunAt!.Value == scheduleAt))
                        .ToList();

                    foreach (var occurrence in missingOccurrences)
                    {
                        var orchestrationInstance = await _manager
                            .ScheduleNewOrchestrationInstanceAsync(
                                RecurringJobIdentity,
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

    private IEnumerable<Instant> ConvertToInstants(IEnumerable<DateTime> scheduleAtInTimeZone)
    {
        return scheduleAtInTimeZone
            .Select(value =>
            {
                var dateTimeInZone = LocalDateTime.FromDateTime(value).InZoneLeniently(_dateTimeZone);
                return dateTimeInZone.ToInstant();
            });
    }
}
