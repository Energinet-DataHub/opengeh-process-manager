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
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Scheduler;

internal class RecurringPlannerHandler(
    ILogger<SchedulerHandler> logger,
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
        // 2.a. Determine "occurences" within the next 24 hours (from the next hour)
        // 2.b. Determine already scheduled instances within the next 24 hours (from the next hour)
        // 2.c. Compare the two lists, and schedule a new orchestration instance for any not appearing in "occurences"

        var now = _clock.GetCurrentInstant();

        var orchestrationDescriptions = await _query
            .GetAllRecurringAsync()
            .ConfigureAwait(false);

        foreach (var orchestrationDescription in orchestrationDescriptions)
        {
            try
            {
            }
            catch (Exception)
            {
                // TODO: Log error
            }
        }
    }
}
