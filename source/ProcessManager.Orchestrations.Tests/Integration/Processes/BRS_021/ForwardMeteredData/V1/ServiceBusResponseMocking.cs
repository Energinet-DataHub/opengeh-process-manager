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

using System.Text.Json;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

public static class ServiceBusResponseMocking
{
    public static async Task WaitOnEnqueueMessagesInEdiAndMockNotifyToProcessManager(
        this ServiceBusListenerMock serviceBusListenerMock,
        IProcessManagerMessageClient processManagerMessageClient,
        Guid orchestrationInstanceId,
        string messageId)
    {
        var verifyServiceBusMessage = await serviceBusListenerMock
            .When(
                message =>
                {
                    if (message.Subject != EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_021_ForwardedMeteredData.V1))
                        return false;

                    var body = EnqueueActorMessagesV1
                        .Parser.ParseJson(message.Body.ToString())!;

                    var forwardMeteredDataAcceptedV1 = JsonSerializer.Deserialize<ForwardMeteredDataAcceptedV1>(body.Data);

                    var messageIdMatches = forwardMeteredDataAcceptedV1?.OriginalActorMessageId == messageId;
                    var orchestrationIdMatches = body.OrchestrationInstanceId == orchestrationInstanceId.ToString();

                    return messageIdMatches && orchestrationIdMatches;
                })
            .VerifyCountAsync(1);
        var messageFound = verifyServiceBusMessage.Wait(TimeSpan.FromSeconds(120));
        messageFound.Should().BeTrue("because EDI should have been asked to enqueue messages");

        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new ForwardMeteredDataNotifyEventV1(
                OrchestrationInstanceId: orchestrationInstanceId.ToString()),
            CancellationToken.None);
    }
}
