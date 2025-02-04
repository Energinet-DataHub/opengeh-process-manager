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
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using FluentAssertions;
using Proto = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1;

public static class ServiceBusResponseMocking
{
    public static async Task WaitAndMockServiceBusMessageToAndFromEdi(
        this ServiceBusListenerMock serviceBusListenerMock,
        IProcessManagerMessageClient processManagerMessageClient,
        Guid orchestrationInstanceId,
        bool successfulResponse = true)
    {
        var verifyServiceBusMessage = await serviceBusListenerMock
            .When(
                message =>
                {
                    if (message.Subject != EnqueueActorMessagesV1.BuildServiceBusMessageSubject(Brs_023_027.V1))
                        return false;

                    var body = EnqueueActorMessagesV1
                        .Parser.ParseJson(message.Body.ToString())!;

                    var calculationCompleted = JsonSerializer.Deserialize<CalculationEnqueueActorMessagesV1>(body.Data);

                    var calculationIdMatches = calculationCompleted!.CalculationId == orchestrationInstanceId;
                    var orchestrationIdMatches = body.OrchestrationInstanceId == orchestrationInstanceId.ToString();

                    return calculationIdMatches && orchestrationIdMatches;
                })
            .VerifyCountAsync(1);
        var messageFound = verifyServiceBusMessage.Wait(TimeSpan.FromSeconds(30));
        messageFound.Should().BeTrue("because EDI should have been asked to enqueue messages");

        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent<CalculationEnqueueActorMessagesCompletedNotifyEventV1>(
                OrchestrationInstanceId: orchestrationInstanceId.ToString(),
                EventName: CalculationEnqueueActorMessagesCompletedNotifyEventV1.EventName,
                Data: new CalculationEnqueueActorMessagesCompletedNotifyEventV1 { Success = successfulResponse }),
            CancellationToken.None);
    }

    public static async Task WaitAndAssertCalculationEnqueueCompletedIntegrationEvent(
        this ServiceBusListenerMock serviceBusListenerMock,
        Guid orchestrationInstanceId,
        Proto.CalculationType calculationType)
    {
        var verifyIntegrationEvent = await serviceBusListenerMock
            .When(
                message =>
                {
                    if (message.Subject != CalculationEnqueueCompletedV1.Descriptor.Name)
                        return false;

                    var calculationEnqueueCompletedV1 = CalculationEnqueueCompletedV1.Parser.ParseFrom(message.Body)!;

                    var calculationIdMatches = calculationEnqueueCompletedV1!.CalculationId == orchestrationInstanceId.ToString();
                    var calculationTypeMatches = calculationEnqueueCompletedV1.CalculationType == calculationType;

                    return calculationIdMatches && calculationTypeMatches;
                })
            .VerifyCountAsync(1);
        var messageFound = verifyIntegrationEvent.Wait(TimeSpan.FromSeconds(30));
        messageFound.Should().BeTrue("because the expected message should be published to the shared integration event topic");
    }
}
