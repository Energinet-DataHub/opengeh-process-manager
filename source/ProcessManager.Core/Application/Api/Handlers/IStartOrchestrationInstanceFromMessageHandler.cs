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

using Azure.Messaging.ServiceBus;

namespace Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;

/// <summary>
/// Defines a handler for starting an orchestration instance based on a received Service Bus message.
/// </summary>
public interface IStartOrchestrationInstanceFromMessageHandler
{
    /// <summary>
    ///  Gets the collection of handlers for starting orchestration instances.
    /// </summary>
    IReadOnlyCollection<IStartOrchestrationInstanceHandler> StartOrchestrationInstanceHandlers { get; }

    /// <summary>
    /// Processes the specified Service Bus message to start an orchestration instance.
    /// </summary>
    /// <param name="message">The received <see cref="ServiceBusReceivedMessage"/> containing orchestration start information.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task HandleAsync(ServiceBusReceivedMessage message);
}
