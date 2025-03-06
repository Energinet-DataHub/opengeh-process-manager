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

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

/// <summary>
/// Event for notifying an orchestration instance.
/// </summary>
/// <param name="OrchestrationInstanceId">The orchestration instance id to notify</param>
/// <param name="EventName">The notify event name (an example could be "EnqueueMessagesCompleted").</param>
public abstract record NotifyOrchestrationInstanceEvent(
    string OrchestrationInstanceId,
    string EventName) :
        IOrchestrationInstanceRequest;

/// <summary>
/// Event (with data) for notifying to an orchestration instance.
/// </summary>
/// <param name="OrchestrationInstanceId">The orchestration instance id to notify</param>
/// <param name="EventName">The notify event name (an example could be "EnqueueActorMessagesCompleted").</param>
/// <param name="Data">Data to send with the notify event (which should be serializable).</param>
/// <typeparam name="TNotifyData">Must be a serializable type.</typeparam>
public abstract record NotifyOrchestrationInstanceEvent<TNotifyData>(
    string OrchestrationInstanceId,
    string EventName,
    TNotifyData Data)
        : NotifyOrchestrationInstanceEvent(OrchestrationInstanceId, EventName)
            where TNotifyData : INotifyDataDto;
