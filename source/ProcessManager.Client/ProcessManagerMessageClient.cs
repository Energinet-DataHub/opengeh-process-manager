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
        MessageCommand<TInputParameterDto> command,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto
    {
        var serviceBusMessage = CreateServiceBusMessage(command);

        return SendServiceBusMessage(
                serviceBusMessage,
                cancellationToken);
    }

    private ServiceBusMessage CreateServiceBusMessage<TInputParameterDto>(
        MessageCommand<TInputParameterDto> command)
    where TInputParameterDto : IInputParameterDto
    {
        var startOrchestration = new StartOrchestrationV1
        {
            OrchestrationName = command.OrchestrationDescriptionUniqueName.Name,
            OrchestrationVersion = command.OrchestrationDescriptionUniqueName.Version,
            StartedByActorId = command.OperatingIdentity.ActorId.ToString(),
            Input = JsonSerializer.Serialize(command.InputParameter),
            InputFormat = "application/json",
        };

        var serviceBusMessage = startOrchestration.ToServiceBusMessage(
            subject: command.OrchestrationDescriptionUniqueName.Name,
            idempotencyKey: command.IdempotencyKey);

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
