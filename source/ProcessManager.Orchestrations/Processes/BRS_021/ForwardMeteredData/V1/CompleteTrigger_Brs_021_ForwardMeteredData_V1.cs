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
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class CompleteTrigger_Brs_021_ForwardMeteredData_V1(
    Orchestration_Brs_021_ForwardMeteredData_V1 orchestration)
{
    private readonly Orchestration_Brs_021_ForwardMeteredData_V1 _orchestration = orchestration;

    /// <summary>
    /// Start a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(CompleteTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.TopicName)}%",
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.Brs021ForwardMeteredDataCompletedSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        var notificationMessage = new MeteredDataForwardedNotifyEventDataV1(Guid.Empty); // deserialize message
        await _orchestration.MessagesEnqueued(new OrchestrationInstanceId(notificationMessage.InstanceId))
            .ConfigureAwait(false);
    }
}

public record MeteredDataForwardedNotifyEventDataV1(
    Guid InstanceId)
    : INotifyDataDto;
