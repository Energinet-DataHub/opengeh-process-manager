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
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1;

// TODO: We have decided to route on the "name" part of the "orchestration description unique name",
// meaning not including the "version" part; this will minimize how often we need to adjust infrastructure
// with regards to "subscriptions". Hence this trigger should not be located within the "V1".
// Also we need a generic way to first parse the "version" of a command and then direct the message to
// the correct "version handler".
public class StartTrigger_Brs_026_V1(
    RequestCalculatedEnergyTimeSeriesHandlerV1 handler)
{
    private readonly RequestCalculatedEnergyTimeSeriesHandlerV1 _handler = handler;

    /// <summary>
    /// Start a BRS-026 request.
    /// </summary>
    [Function(nameof(StartTrigger_Brs_026_V1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{ProcessManagerStartTopicOptions.SectionName}:{nameof(ProcessManagerStartTopicOptions.TopicName)}%",
            $"%{ProcessManagerStartTopicOptions.SectionName}:{nameof(ProcessManagerStartTopicOptions.Brs026SubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        await _handler.HandleAsync(message)
            .ConfigureAwait(false);
    }
}
