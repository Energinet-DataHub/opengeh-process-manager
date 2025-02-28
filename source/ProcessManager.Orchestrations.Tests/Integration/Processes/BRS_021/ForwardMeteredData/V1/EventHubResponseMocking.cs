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

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Energinet.DataHub.Core.FunctionApp.TestCommon.EventHub.ListenerMock;
using Energinet.DataHub.Measurements.Contracts;
using FluentAssertions;
using Google.Protobuf;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

public static class EventHubResponseMocking
{
    public static async Task<bool> AssertAndMockEventHubMessageToAndFromMeasurementsAsync(
        this EventHubListenerMock eventHubListenerMock,
        EventHubProducerClient eventHubProducerClient,
        Guid orchestrationInstanceId,
        string transactionId)
    {
        var passableEvents = eventHubListenerMock.ReceivedEvents.Where(
            e => PersistSubmittedTransaction.Parser.ParseFrom(e.Body.ToArray()) != null);
        var passableEvent = passableEvents.Should().ContainSingle().Subject;

        var persistedTransaction = PersistSubmittedTransaction.Parser.ParseFrom(passableEvent.Body.ToArray());

        var orchestrationIdMatches = persistedTransaction.OrchestrationInstanceId == orchestrationInstanceId.ToString();
        var transactionIdIdMatches = persistedTransaction.TransactionId == transactionId;

        if (!orchestrationIdMatches || !transactionIdIdMatches)
            return false;

        var data = new EventData(persistedTransaction.ToByteArray());
        await eventHubProducerClient.SendAsync([data], CancellationToken.None);
        return true;
    }
}
