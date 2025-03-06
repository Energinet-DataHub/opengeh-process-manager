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

using Azure.Messaging.EventHubs;
using Energinet.DataHub.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;

public class EnqueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1(
    EnqueueMeteredDataHandlerV1 handler)
{
    private readonly EnqueueMeteredDataHandlerV1 _handler = handler;

    /// <summary>
    /// Enqueue Messages for BRS-021.
    /// </summary>
    [Function(nameof(EnqueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [EventHubTrigger(
            $"%{ProcessManagerEventHubOptions.SectionName}:{nameof(ProcessManagerEventHubOptions.EventHubName)}%",
            IsBatched = false,
            Connection = ProcessManagerEventHubOptions.SectionName)]
        EventData message)
    {
        var notify = Brs021ForwardMeteredDataNotifyV1.Parser.ParseFrom(message.EventBody.ToArray());
        if (notify == null)
        {
            throw new InvalidOperationException("Failed to deserialize message");
        }

        var orchestrationInstanceId = new Core.Domain.OrchestrationInstance.OrchestrationInstanceId(
            Guid.Parse(notify.OrchestrationInstanceId));
        await _handler.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
    }
}
