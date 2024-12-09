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
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

public class StartForwardMeteredDataTriggerV1(
    ILogger<StartForwardMeteredDataTriggerV1> logger,
    StartForwardMeteredDataHandler handler)
{
    private readonly ILogger<StartForwardMeteredDataTriggerV1> _logger = logger;
    private readonly StartForwardMeteredDataHandler _handler = handler;

    /// <summary>
    /// Start a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(StartForwardMeteredDataTriggerV1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.TopicName)}%",
            $"%{ProcessManagerTopicOptions.SectionName}:{nameof(ProcessManagerTopicOptions.Brs021ForwardMeteredDataSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        using var serviceBusMessageLoggerScope = _logger.BeginScope(new
        {
            ServiceBusMessage = new
            {
                message.MessageId,
                message.CorrelationId,
                message.Subject,
            },
        });

        var jsonMessage = message.Body.ToString();
        var startOrchestrationDto = StartOrchestrationDto.Parser.ParseJson(jsonMessage);
        using var startOrchestrationLoggerScope = _logger.BeginScope(new
        {
            StartOrchestration = new
            {
                startOrchestrationDto.OrchestrationName,
                startOrchestrationDto.OrchestrationVersion,
            },
        });

        var meteredDataForMeasurementPointMessageInput = JsonSerializer.Deserialize<MeteredDataForMeasurementPointMessageInputV1>(startOrchestrationDto.JsonInput);
        if (meteredDataForMeasurementPointMessageInput is null)
        {
            _logger.LogWarning($"Unable to deserialize {nameof(startOrchestrationDto.JsonInput)} to {nameof(MeteredDataForMeasurementPointMessageInputV1)} type:{Environment.NewLine}{0}", startOrchestrationDto.JsonInput);
            throw new ArgumentException($"Unable to deserialize {nameof(startOrchestrationDto.JsonInput)} to {nameof(MeteredDataForMeasurementPointMessageInputV1)} type");
        }

        await _handler.StartForwardMeteredDataAsync(meteredDataForMeasurementPointMessageInput)
            .ConfigureAwait(false);
    }
}
