﻿// Copyright 2020 Energinet DataHub A/S
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

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

public interface IStartOrchestrationInstanceCommands
{
    /// <summary>
    /// Start a new instance of an orchestration.
    /// </summary>
    Task<OrchestrationInstanceId> StartNewOrchestrationInstanceAsync(
        OperatingIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName);

    /// <summary>
    /// Start a new instance of an orchestration with input parameter and the
    /// possibility to skip steps.
    /// </summary>
    Task<OrchestrationInstanceId> StartNewOrchestrationInstanceAsync<TParameter>(
        OperatingIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName,
        TParameter inputParameter,
        IReadOnlyCollection<int> skipStepsBySequence)
            where TParameter : class;

    /// <summary>
    /// Schedule a new instance of an orchestration.
    /// </summary>
    Task<OrchestrationInstanceId> ScheduleNewOrchestrationInstanceAsync(
        UserIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName,
        Instant runAt);

    /// <summary>
    /// Schedule a new instance of an orchestration with input parameter and the
    /// possibility to skip steps.
    /// </summary>
    Task<OrchestrationInstanceId> ScheduleNewOrchestrationInstanceAsync<TParameter>(
        UserIdentity identity,
        OrchestrationDescriptionUniqueName uniqueName,
        TParameter inputParameter,
        Instant runAt,
        IReadOnlyCollection<int> skipStepsBySequence)
            where TParameter : class;
}
