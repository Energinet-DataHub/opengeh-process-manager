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

using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

/// <summary>
/// Defines a handler for starting an orchestration instance.
/// </summary>
public interface IStartOrchestrationInstanceHandler
{
    /// <summary>
    /// Determines whether this handler can process the specified orchestration start request.
    /// </summary>
    /// <param name="startOrchestration">The orchestration start request.</param>
    /// <returns><see langword="true"/> if this handler can handle the specified request; otherwise, <see langword="false"/>.</returns>
    bool CanHandle(StartOrchestrationInstanceV1 startOrchestration);

    /// <summary>
    /// Processes the orchestration start request asynchronously.
    /// </summary>
    /// <param name="startOrchestration">The orchestration start request.</param>
    /// <param name="idempotencyKey">The idempotency key used to ensure operation idempotence.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(StartOrchestrationInstanceV1 startOrchestration, IdempotencyKey idempotencyKey);
}
