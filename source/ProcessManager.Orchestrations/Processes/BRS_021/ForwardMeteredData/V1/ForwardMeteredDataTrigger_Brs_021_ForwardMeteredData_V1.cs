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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class ForwardMeteredDataTrigger_Brs_021_ForwardMeteredData_V1(
    Orchestration_Brs_021_ForwardMeteredData_V1 orchestration)
{
    private readonly Orchestration_Brs_021_ForwardMeteredData_V1 _orchestration = orchestration;

    /// <summary>
    /// Continue a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(ForwardMeteredDataTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run([EventHubTrigger("src", Connection = "EventHubConnection")] string input)
    {
        var notificationEvent = JsonSerializer.Deserialize<MeteredDataStoredNotifyEventDataV1>(input);
        if (notificationEvent == null)
        {
            throw new InvalidOperationException("Failed to deserialize event message");
        }

        await _orchestration.ForwardMeteredData(new OrchestrationInstanceId(notificationEvent.InstanceId))
            .ConfigureAwait(false);
    }
}

public record MeteredDataStoredNotifyEventDataV1(
    Guid InstanceId)
    : INotifyDataDto;
