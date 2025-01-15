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
using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Google.Protobuf;
using Microsoft.Extensions.Azure;

namespace Energinet.DataHub.ProcessManager.Client;

public class ProcessManagerMessageClient(
    IAzureClientFactory<ServiceBusSender> serviceBusFactory)
        : IProcessManagerMessageClient
{
    private readonly ServiceBusSender _serviceBusSender = serviceBusFactory.CreateClient(ServiceBusSenderNames.ProcessManagerTopic);

    public Task StartNewOrchestrationInstanceAsync<TInputParameterDto>(
        StartOrchestrationInstanceMessageCommand<TInputParameterDto> command,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto
    {
        var serviceBusMessage = CreateStartOrchestrationInstanceServiceBusMessage(command);

        return SendServiceBusMessage(
                serviceBusMessage,
                cancellationToken);
    }

    public Task NotifyOrchestrationInstanceAsync(
        NotifyOrchestrationInstanceEvent notifyEvent,
        CancellationToken cancellationToken)
    {
        var serviceBusMessage = CreateNotifyOrchestrationInstanceServiceBusMessage<int?>(
            notifyEvent,
            data: null);

        return SendServiceBusMessage(
            serviceBusMessage,
            cancellationToken);
    }

    public Task NotifyOrchestrationInstanceAsync<TNotifyDataDto>(
        NotifyOrchestrationInstanceEvent<TNotifyDataDto> notifyEvent,
        CancellationToken cancellationToken)
        where TNotifyDataDto : INotifyDataDto
    {
        var serviceBusMessage = CreateNotifyOrchestrationInstanceServiceBusMessage(
            notifyEvent,
            data: notifyEvent.Data);

        return SendServiceBusMessage(
            serviceBusMessage,
            cancellationToken);
    }

    private ServiceBusMessage CreateStartOrchestrationInstanceServiceBusMessage<TInputParameterDto>(
        StartOrchestrationInstanceMessageCommand<TInputParameterDto> command)
    where TInputParameterDto : IInputParameterDto
    {
        var startOrchestration = new StartOrchestrationInstanceV1
        {
            OrchestrationName = command.OrchestrationDescriptionUniqueName.Name,
            OrchestrationVersion = command.OrchestrationDescriptionUniqueName.Version,
            StartedByActorId = command.OperatingIdentity.ActorId.ToString(),
            Input = JsonSerializer.Serialize(command.InputParameter),
            InputFormat = StartOrchestrationInstanceInputFormatV1.Json,
        };

        var serviceBusMessage = startOrchestration.ToServiceBusMessage(
            subject: command.OrchestrationDescriptionUniqueName.Name,
            idempotencyKey: command.IdempotencyKey);

        return serviceBusMessage;
    }

    private ServiceBusMessage CreateNotifyOrchestrationInstanceServiceBusMessage<TNotifyData>(
        NotifyOrchestrationInstanceEvent notifyEvent,
        TNotifyData? data)
    {
        var notifyOrchestration = new NotifyOrchestrationInstanceV1
        {
            OrchestrationInstanceId = notifyEvent.OrchestrationInstanceId,
            EventName = notifyEvent.EventName,
        };

        if (data is not null)
        {
            notifyOrchestration.Data = new NotifyOrchestrationInstanceDataV1
            {
                Data = JsonSerializer.Serialize(data),
                DataFormat = NotifyOrchestrationInstanceDataFormatV1.Json,
                DataType = typeof(TNotifyData).Name,
            };
        }

        var serviceBusMessage = notifyOrchestration.ToServiceBusMessage(
            subject: "NotifyOrchestration",
            idempotencyKey: Guid.NewGuid().ToString());

        return serviceBusMessage;
    }

    private async Task SendServiceBusMessage(
        ServiceBusMessage serviceBusMessage,
        CancellationToken cancellationToken)
    {
        await _serviceBusSender.SendMessageAsync(serviceBusMessage, cancellationToken)
            .ConfigureAwait(false);
    }
}
