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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Application.Scheduling;

/// <summary>
/// Queries necessary to handle the planning of recurring orchestrations.
/// </summary>
public interface IRecurringOrchestrationQueries
{
    /// <summary>
    /// Get all enabled orchestration descriptions that are recurring.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationDescription>> SearchRecurringOrchestrationDescriptionsAsync();

    /// <summary>
    /// Get scheduled orchestration instances by their related orchestration definition unique name,
    /// and by the time range they are are scheduled to run within.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstance>> SearchScheduledOrchestrationInstancesAsync(
        OrchestrationDescriptionUniqueName uniqueName,
        Instant runAtOrLater,
        Instant runAtOrEarlier);
}
