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

namespace Energinet.DataHub.ProcessManager.Core.Application.Registration;

/// <summary>
/// Read/write access to the orchestration register.
/// </summary>
internal interface IOrchestrationRegister
{
    Task<IReadOnlyCollection<OrchestrationDescription>> GetAllByHostNameAsync(string hostName);

    /// <summary>
    /// Determine if <paramref name="newDescription"/> is unknown to the register and needs to be registered;
    /// or if it was previously disabled and needs to be enabled;
    /// or if any refreshable property has changed.
    /// </summary>
    /// <param name="existingDescription">Orchestration description as described in the register.</param>
    /// <param name="newDescription">Orchestration description as described by the application host.</param>
    /// <returns><see langword="true"/> if the orchestration description should be registered or updated; otherwise <see langword="false"/>.</returns>
    bool ShouldRegisterOrUpdate(OrchestrationDescription? existingDescription, OrchestrationDescription newDescription);

    /// <summary>
    /// Durable Functions orchestration host's can use this method to register or update the orchestrations
    /// they host.
    /// </summary>
    /// <param name="newDescription">Orchestration description as described by the application host.</param>
    /// <param name="hostName">Name of the application host.</param>
    Task RegisterOrUpdateAsync(OrchestrationDescription newDescription, string hostName);

    /// <summary>
    /// Durable Functions orchestration host's can use this method to disable orchestrations they don't host anymore
    /// or want to disable for other reasons.
    /// </summary>
    /// <param name="registerDescription">Orchestration description as described in the register.</param>
    Task DeregisterAsync(OrchestrationDescription registerDescription);
}
