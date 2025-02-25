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
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V2.Handlers;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V2.Triggers;

public class EdiEnqueuedMeteredDataTrigger_Brs_021_ForwardMeteredData_V2(
    EdiEnqueuedMeteredDataHandler ediEnqueuedMeteredDataHandler)
{
    private readonly EdiEnqueuedMeteredDataHandler _ediEnqueuedMeteredDataHandler = ediEnqueuedMeteredDataHandler;

    /// <summary>
    /// Terminate a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(EdiEnqueuedMeteredDataTrigger_Brs_021_ForwardMeteredData_V2))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.TopicName)}%",
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions) + "We need a new subscription?"}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        string message)
    {
        // TODO: Correct type for which to deserialize to
        var notification = JsonConvert.DeserializeObject<NotifyOrchestrationInstanceV1>(message);
        if (notification == null) throw new InvalidOperationException("Failed to deserialize message");

        var orchestrationInstanceId = new Core.Domain.OrchestrationInstance.OrchestrationInstanceId(Guid.Parse(notification.OrchestrationInstanceId));
        await _ediEnqueuedMeteredDataHandler.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
    }
}
