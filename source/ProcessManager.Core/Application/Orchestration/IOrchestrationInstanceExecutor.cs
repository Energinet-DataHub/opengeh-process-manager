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

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

/// <summary>
/// Abstracts the execution of orchestration instances from technology specific implementations.
/// </summary>
internal interface IOrchestrationInstanceExecutor
{
    /// <summary>
    /// Start a new orchestration instance using the orchestration instance ID.
    /// </summary>
    /// <remarks>
    /// This operation doesn't start a new orchestration instance if one with
    /// the given orchestration instance ID already exists.
    /// </remarks>
    /// <param name="orchestrationDescription"></param>
    /// <param name="orchestrationInstance"></param>
    /// <returns>True if a new orchestration instance was started; false if an orchestration instance with the ID already exists.</returns>
    Task<bool> StartNewOrchestrationInstanceAsync(OrchestrationDescription orchestrationDescription, OrchestrationInstance orchestrationInstance);

    /// <summary>
    /// Send a notify event to a running orchestration instance.
    /// </summary>
    Task NotifyOrchestrationInstanceAsync<TData>(OrchestrationInstanceId id, string eventName, TData? data)
        where TData : class;
}
