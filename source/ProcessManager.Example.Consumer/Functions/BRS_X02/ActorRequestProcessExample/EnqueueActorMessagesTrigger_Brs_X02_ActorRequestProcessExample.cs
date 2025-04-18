﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Example.Consumer.Extensions.Options;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.ActorRequestProcessExample.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Example.Consumer.Functions.BRS_X02.ActorRequestProcessExample;

/// <summary>
/// Service Bus trigger listening for EnqueueActorMessages events sent from the Process Manager.
/// </summary>
public class EnqueueActorMessagesTrigger_Brs_X02_ActorRequestProcessExample(
    ILogger<EnqueueActorMessagesTrigger_Brs_X02_ActorRequestProcessExample> logger,
    IProcessManagerMessageClient messageClient)
{
    private readonly ILogger<EnqueueActorMessagesTrigger_Brs_X02_ActorRequestProcessExample> _logger = logger;
    private readonly IProcessManagerMessageClient _messageClient = messageClient;

    [Function(nameof(EnqueueActorMessagesTrigger_Brs_X02_ActorRequestProcessExample))]
    public Task Run(
        [ServiceBusTrigger(
            $"%{EdiTopicOptions.SectionName}:{nameof(EdiTopicOptions.Name)}%",
            $"%{EdiTopicOptions.SectionName}:{nameof(EdiTopicOptions.EnqueueBrsX02ActorRequestProcessExampleSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        var majorVersion = message.GetMajorVersion();

        if (majorVersion == EnqueueActorMessagesV1.MajorVersion)
            return HandleV1(message);

        throw new ArgumentOutOfRangeException(
            nameof(majorVersion),
            majorVersion,
            "Unknown major version on received service bus message.")
        {
            Data =
            {
                { "MessageId", message.MessageId },
                { "Subject", message.Subject },
                { "ApplicationProperties", message.ApplicationProperties },
            },
        };
    }

    private async Task HandleV1(ServiceBusReceivedMessage message)
    {
        using var serviceBusMessageScope = _logger.BeginScope(new
        {
            ServiceBusMessage = new
            {
                message.MessageId,
                message.Subject,
                message.ApplicationProperties,
            },
        });

        var enqueueActorMessagesV1 = message.ParseBody<EnqueueActorMessagesV1>();

        using var enqueueActorMessagesScope = _logger.BeginScope(new
        {
            EnqueueActorMessages = enqueueActorMessagesV1,
        });

        switch (enqueueActorMessagesV1.DataType)
        {
            case nameof(ActorRequestProcessExampleEnqueueDataV1):
            {
                var enqueueData = enqueueActorMessagesV1.ParseData<ActorRequestProcessExampleEnqueueDataV1>();
                using var enqueueDataScope = _logger.BeginScope(new
                {
                    EnqueueData = enqueueData,
                });

                _logger.LogInformation(
                    "Received {Subject} message with message id {MessageId}. Data: {Data}",
                    message.Subject,
                    message.MessageId,
                    enqueueData);
                break;
            }

            case nameof(ActorRequestProcessExampleEnqueueRejectedDataV1):
            {
                var enqueueRejectedData = enqueueActorMessagesV1.ParseData<ActorRequestProcessExampleEnqueueRejectedDataV1>();

                using var enqueueDataScope = _logger.BeginScope(new
                {
                    EnqueueRejectedData = enqueueRejectedData,
                });

                _logger.LogInformation(
                    "Received {Subject} reject message with message id {MessageId}. Rejected data: {RejectedData}",
                    message.Subject,
                    message.MessageId,
                    enqueueRejectedData);
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(enqueueActorMessagesV1.DataType),
                    enqueueActorMessagesV1.DataType,
                    $"Unknown data type on received {nameof(EnqueueActorMessagesV1)}.");
        }

        await _messageClient.NotifyOrchestrationInstanceAsync(
                new ActorRequestProcessExampleNotifyEventV1(
                    OrchestrationInstanceId: enqueueActorMessagesV1.OrchestrationInstanceId),
                CancellationToken.None)
            .ConfigureAwait(false);
    }
}
