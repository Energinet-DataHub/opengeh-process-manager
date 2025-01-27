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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1;

public static class StartersAndMockers
{
    public static async Task<Guid> StartCalculationAsync(
        this IProcessManagerClient processManagerClient,
        UserIdentityDto? userIdentity = null,
        CalculationType calculationType = CalculationType.WholesaleFixing)
    {
        var inputParameter = new CalculationInputV1(
            calculationType,
            GridAreaCodes: new[] { "804" },
            PeriodStartDate: new DateTimeOffset(2023, 1, 31, 23, 0, 0, TimeSpan.Zero),
            PeriodEndDate: new DateTimeOffset(2023, 2, 28, 23, 0, 0, TimeSpan.Zero),
            IsInternalCalculation: false);
        var orchestrationInstanceId = await processManagerClient
            .StartNewOrchestrationInstanceAsync(
                new StartCalculationCommandV1(
                    userIdentity ?? new UserIdentityDto(
                        UserId: Guid.NewGuid(),
                        ActorId: Guid.NewGuid()),
                    inputParameter),
                CancellationToken.None);

        return orchestrationInstanceId;
    }

    public static async Task WaitAndMockServiceBusMessageToAndFromEdi(
        this ServiceBusListenerMock serviceBusListenerMock,
        IProcessManagerMessageClient processManagerMessageClient,
        Guid orchestrationInstanceId,
        CalculationType calculationType = CalculationType.WholesaleFixing)
    {
        var verifyServiceBusMessage = await serviceBusListenerMock
            .When(
                message =>
                {
                    if (message.Subject != $"Enqueue_{Brs_023_027.Name.ToLower()}")
                        return false;

                    var body = Energinet.DataHub.ProcessManager.Abstractions.Contracts.EnqueueActorMessagesV1
                        .Parser.ParseJson(message.Body.ToString())!;

                    var calculationCompleted = JsonSerializer.Deserialize<CalculatedDataForCalculationTypeV1>(body.Data);

                    var typeMatches = calculationCompleted!.CalculationType == calculationType;
                    var calculationIdMatches = calculationCompleted!.CalculationId == orchestrationInstanceId;
                    var orchestrationIdMatches = body.OrchestrationInstanceId == orchestrationInstanceId.ToString();

                    return typeMatches && calculationIdMatches && orchestrationIdMatches;
                })
            .VerifyCountAsync(1);
        var messageFound = verifyServiceBusMessage.Wait(TimeSpan.FromSeconds(30));
        messageFound.Should().BeTrue("because the expected message should be sent on the ServiceBus");

        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent<NotifyEnqueueFinishedV1>(
                OrchestrationInstanceId: orchestrationInstanceId.ToString(),
                EventName: NotifyEnqueueFinishedV1.EventName,
                Data: new NotifyEnqueueFinishedV1 { Success = true }),
            CancellationToken.None);
    }
}
