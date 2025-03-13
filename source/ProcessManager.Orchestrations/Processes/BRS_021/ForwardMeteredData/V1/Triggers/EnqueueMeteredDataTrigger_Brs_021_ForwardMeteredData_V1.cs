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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;

public class EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1(
    EnqueueMeteredDataHandlerV1 handler)
{
    private readonly EnqueueMeteredDataHandlerV1 _handler = handler;

    /// <summary>
    /// Enqueue Messages for BRS-021.
    /// </summary>
    [Function(nameof(EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1))]
    [ExponentialBackoffRetry(5, "00:00:01", "00:01:00")]
    public async Task Run(
        [EventHubTrigger(
            $"%{ProcessManagerEventHubOptions.SectionName}:{nameof(ProcessManagerEventHubOptions.EventHubName)}%",
            IsBatched = false,
            Connection = ProcessManagerEventHubOptions.SectionName)]
        EventData message)
    {
        var brs021ForwardMeteredDataNotifyVersion = Brs021ForwardMeteredDataNotifyVersion.Parser.ParseFrom(message.EventBody);

        if (brs021ForwardMeteredDataNotifyVersion is null)
            throw new InvalidOperationException($"Failed to deserialize message to {nameof(Brs021ForwardMeteredDataNotifyVersion)}.");

        var orchestrationInstanceId = brs021ForwardMeteredDataNotifyVersion.Version switch
        {
            "1" or "v1" => HandleV1(message.EventBody),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(Brs021ForwardMeteredDataNotifyVersion),
                actualValue: brs021ForwardMeteredDataNotifyVersion.Version,
                message: $"Unhandled {nameof(Brs021ForwardMeteredDataNotifyVersion)} version."),
        };

        await _handler.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
    }

    private Core.Domain.OrchestrationInstance.OrchestrationInstanceId HandleV1(BinaryData messageEventBody)
    {
        var notifyV1 = Brs021ForwardMeteredDataNotifyV1.Parser.ParseFrom(messageEventBody);

        if (notifyV1 is null)
            throw new InvalidOperationException($"Failed to deserialize message to {nameof(Brs021ForwardMeteredDataNotifyV1)}.");

        return new Core.Domain.OrchestrationInstance.OrchestrationInstanceId(Guid.Parse(notifyV1.OrchestrationInstanceId));
    }
}
