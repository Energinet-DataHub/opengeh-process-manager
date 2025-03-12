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

using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;

public class TerminateTrigger_Brs_021_ForwardMeteredData_V1(
    TerminateForwardMeteredDataHandlerV1 terminateForwardMeteredDataHandlerV1)
{
    private readonly TerminateForwardMeteredDataHandlerV1 _terminateForwardMeteredDataHandlerV1 = terminateForwardMeteredDataHandlerV1;

    /// <summary>
    /// Terminate a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(TerminateTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{Brs021ForwardMeteredDataTopicOptions.SectionName}:{nameof(Brs021ForwardMeteredDataTopicOptions.NotifyTopicName)}%",
            $"%{Brs021ForwardMeteredDataTopicOptions.SectionName}:{nameof(Brs021ForwardMeteredDataTopicOptions.NotifySubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        string message)
    {
        var notify = NotifyOrchestrationInstanceV1.Parser.ParseJson(message);
        if (notify is not { EventName: ForwardMeteredDataNotifyEventV1.OrchestrationInstanceEventName })
        {
            throw new InvalidOperationException("Failed to deserialize message");
        }

        var orchestrationInstanceId = new Core.Domain.OrchestrationInstance.OrchestrationInstanceId(Guid.Parse(notify.OrchestrationInstanceId));
        await _terminateForwardMeteredDataHandlerV1.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
    }
}
