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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class MeasurementReceivedMeteredDataTrigger_Brs_021_ForwardMeteredData_V1(
    Orchestration_Brs_021_ForwardMeteredData_V1 orchestrationBrs021ForwardMeteredDataV1)
{
    private readonly Orchestration_Brs_021_ForwardMeteredData_V1 _orchestrationBrs021ForwardMeteredDataV1 = orchestrationBrs021ForwardMeteredDataV1;

    /// <summary>
    /// Enqueue Messages for BRS-021.
    /// </summary>
    [Function(nameof(MeasurementReceivedMeteredDataTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [EventHubTrigger(
            "%EventHubName%",
            Connection = "EventHubConnection")]
        string message)
    {
        var notification = JsonConvert.DeserializeObject<MeasurementReceivedMeteredDataNotification>(message);
        if (notification == null) throw new InvalidOperationException("Failed to deserialize message");

        await _orchestrationBrs021ForwardMeteredDataV1.EnqueueMessages(notification.OrchestrationId)
            .ConfigureAwait(false);
    }
}

public record MeasurementReceivedMeteredDataNotification(OrchestrationInstanceId OrchestrationId);
