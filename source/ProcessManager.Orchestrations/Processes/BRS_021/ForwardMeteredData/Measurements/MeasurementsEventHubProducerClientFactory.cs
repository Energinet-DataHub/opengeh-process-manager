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
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;

public class MeasurementsEventHubProducerClientFactory(
    IOptions<ProcessManagerComponentsOptions> options,
    IAzureClientFactory<EventHubProducerClient> eventHubClientFactory)
{
    private readonly ProcessManagerComponentsOptions _options = options.Value;
    private readonly IAzureClientFactory<EventHubProducerClient> _eventHubProducerFactory = eventHubClientFactory;

    /// <summary>
    /// Create an Event Hub producer client to messages to the Measurements subsystem.
    /// <remarks>
    /// If mocking dependencies are allowed for the current environment, then a mock will be returned
    /// if the given <paramref name="meteringPointId"/> parameter is a test id.
    /// </remarks>
    /// </summary>
    public EventHubProducerClient Create(string meteringPointId)
    {
        if (!_options.AllowMockDependenciesForTests)
            return CreateMeasurementsEventHubProducerClient();

        if (meteringPointId.IsTestMeteringPointId())
            return new EventHubProducerClientMock();

        return CreateMeasurementsEventHubProducerClient();
    }

    private EventHubProducerClient CreateMeasurementsEventHubProducerClient()
    {
        return _eventHubProducerFactory.CreateClient(EventHubProducerClientNames.MeasurementsEventHub);
    }

    /// <summary>
    /// An Event Hub producer client mock implementation, that does not send any messages.
    /// </summary>
    private class EventHubProducerClientMock : EventHubProducerClient
    {
        public override Task SendAsync(
            IEnumerable<EventData> eventBatch,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task SendAsync(
            IEnumerable<EventData> eventBatch,
            SendEventOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task SendAsync(
            EventDataBatch eventBatch,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
