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
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Extensions.Options;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Api;

public class NotifyOrchestrationInstanceTrigger(
    INotifyOrchestrationInstanceCommands notifyOrchestrationCommands)
{
    private readonly INotifyOrchestrationInstanceCommands _notifyOrchestrationCommands = notifyOrchestrationCommands;

    /// <summary>
    /// Start a BRS-026 request.
    /// </summary>
    [Function(nameof(NotifyOrchestrationInstanceTrigger))]
    public Task Run(
        [ServiceBusTrigger(
            $"%{NotifyOrchestrationInstanceOptions.SectionName}:{nameof(NotifyOrchestrationInstanceOptions.TopicName)}%",
            $"%{NotifyOrchestrationInstanceOptions.SectionName}:{nameof(NotifyOrchestrationInstanceOptions.NotifyOrchestrationInstanceSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        var t = NotifyOrchestrationInstanceV1.Parser.ParseJson(message.Body.ToString());

        ArgumentNullException.ThrowIfNull(t);

        var data = t.Data != null
            ? t.Data
            : null;

        return _notifyOrchestrationCommands.NotifyOrchestrationInstanceAsync(
            new OrchestrationInstanceId(Guid.Parse(t.OrchestrationInstanceId)),
            t.EventName,
            data);
    }
}
