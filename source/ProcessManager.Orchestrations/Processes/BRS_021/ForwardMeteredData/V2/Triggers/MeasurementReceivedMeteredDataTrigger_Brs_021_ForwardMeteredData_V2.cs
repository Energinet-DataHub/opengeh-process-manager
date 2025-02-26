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
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.InternalProcesses.MigrateCalculationsFromWholesale.Wholesale.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V2.Handlers;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V2.Triggers;

public class MeasurementReceivedMeteredDataTrigger_Brs_021_ForwardMeteredData_V2(
    MeasurementReceivedMeteredDataTriggerHandlerV2 handler)
{
    private readonly MeasurementReceivedMeteredDataTriggerHandlerV2 _handler = handler;

    /// <summary>
    /// Enqueue Messages for BRS-021.
    /// </summary>
    [Function(nameof(MeasurementReceivedMeteredDataTrigger_Brs_021_ForwardMeteredData_V2))]
    public async Task Run(
        [EventHubTrigger(
            $"%{MeasurementsMeteredDataClientOptions.SectionName}:{nameof(MeasurementsMeteredDataClientOptions.ProcessManagerEventHubName)}%%")]
        EventData[] message)
    {
        var notification = JsonConvert.DeserializeObject<MeasurementReceivedMeteredDataNotification>(message[0].EventBody.ToString());
        if (notification == null) throw new InvalidOperationException("Failed to deserialize message");

        var orchestrationInstanceId = new Core.Domain.OrchestrationInstance.OrchestrationInstanceId(Guid.Parse(notification.OrchestrationId.Id));
        await _handler.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
    }
}

public record MeasurementReceivedMeteredDataNotification(OrchestrationInstanceId OrchestrationId);
