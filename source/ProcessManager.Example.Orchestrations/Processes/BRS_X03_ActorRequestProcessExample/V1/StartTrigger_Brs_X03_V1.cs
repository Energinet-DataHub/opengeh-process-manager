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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1;

internal class StartTrigger_Brs_X03_V1(
    StartNotifyOrchestrationInstanceExampleHandlerV1 handler)
{
    private readonly StartNotifyOrchestrationInstanceExampleHandlerV1 _handler = handler;

    /// <summary>
    /// Start a BRS-028 request.
    /// </summary>
    [Function(nameof(StartTrigger_Brs_X03_V1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.TopicName)}%",
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.BrsX02SubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        await _handler.HandleAsync(message)
            .ConfigureAwait(false);
    }
}
