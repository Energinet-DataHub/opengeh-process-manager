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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;

namespace Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;

public interface IEnqueueActorMessagesClient
{
    /// <summary>
    /// Enqueue a message to an actor
    /// </summary>
    /// <param name="orchestration">The unique name of the orchestration that initiated the enqueue</param>
    /// <param name="orchestrationInstanceId">The unique ID for the orchestration</param>
    /// <param name="orchestrationStartedBy">The identity that initiated the orchestration</param>
    /// <param name="idempotencyKey">A unique key that MUST not change if the same ´data´ is being supplied</param>
    /// <param name="data">Is the payload from witch the message to the actor is generated.</param>
    public Task EnqueueAsync<TData>(
        OrchestrationDescriptionUniqueNameDto orchestration,
        Guid orchestrationInstanceId,
        IOperatingIdentityDto orchestrationStartedBy,
        Guid idempotencyKey,
        TData data)
            where TData : INotifyEnqueueDataDto;
}
