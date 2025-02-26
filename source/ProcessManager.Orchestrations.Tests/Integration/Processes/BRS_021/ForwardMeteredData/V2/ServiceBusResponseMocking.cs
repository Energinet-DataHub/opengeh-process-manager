using System.Text.Json;
using Energinet.DataHub.Core.FunctionApp.TestCommon.ServiceBus.ListenerMock;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V2;
using FluentAssertions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V2;

public static class ServiceBusResponseMocking
{
    public static async Task WaitAndMockServiceBusMessageToAndFromEdi(
        this ServiceBusListenerMock serviceBusListenerMock,
        IProcessManagerMessageClient processManagerMessageClient,
        Guid orchestrationInstanceId,
        string messageId)
    {
        var verifyServiceBusMessage = await serviceBusListenerMock
            .When(
                message =>
                {
                    if (message.Subject != EnqueueActorMessagesV1.BuildServiceBusMessageSubject(OrchestrationDescriptionBuilder.UniqueName))
                        return false;

                    var body = EnqueueActorMessagesV1
                        .Parser.ParseJson(message.Body.ToString())!;

                    var meteredDateForMeringPointAccepted = JsonSerializer.Deserialize<MeteredDataForMeteringPointAcceptedV1>(body.Data);

                    var messageIdMatches = meteredDateForMeringPointAccepted?.MessageId == messageId;
                    var orchestrationIdMatches = body.OrchestrationInstanceId == orchestrationInstanceId.ToString();

                    return messageIdMatches && orchestrationIdMatches;
                })
            .VerifyCountAsync(1);
        var messageFound = verifyServiceBusMessage.Wait(TimeSpan.FromSeconds(30));
        messageFound.Should().BeTrue("because EDI should have been asked to enqueue messages");

        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent(
                OrchestrationInstanceId: orchestrationInstanceId.ToString(),
                EventName: MeteredDataForMeteringPointMessagesEnqueuedNotifyEventsV1.MeteredDataForMeteringPointMessagesEnqueuedCompleted),
            CancellationToken.None);
    }
}
