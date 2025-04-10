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

using Azure.Messaging.ServiceBus;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;

public class EdiTopicServiceBusSenderFactory(
    ILogger<EdiTopicServiceBusSenderFactory> logger,
    IOptions<ProcessManagerOptions> options,
    IAzureClientFactory<ServiceBusSender> serviceBusFactory)
{
    private readonly ILogger<EdiTopicServiceBusSenderFactory> _logger = logger;
    private readonly IOptions<ProcessManagerOptions> _options = options;
    private readonly IAzureClientFactory<ServiceBusSender> _serviceBusFactory = serviceBusFactory;

    /// <summary>
    /// Create a service bus sender to send enqueue messages.
    /// <remarks>
    /// If mocking dependencies are allowed for the current environment, then a mock will be returned
    /// if the given <paramref name="data"/> parameter has an OriginalActorMessageId, and it is a test id.
    /// </remarks>
    /// </summary>
    public ServiceBusSender CreateSender(IEnqueueDataDto data)
    {
        if (!_options.Value.AllowMockDependenciesForTests)
            return CreateEdiServiceBusSender();

        var originalActorMessageId = data switch
        {
            IEnqueueAcceptedDataDto d => d.OriginalActorMessageId,
            IEnqueueRejectedDataDto d => d.OriginalActorMessageId,
            _ => null,
        };

        // If the message is a test message, we don't want to send it to EDI. This is used by tests (subsystem test,
        // load tests) to not pollute other subsystems, and should only be enabled on dev/test environments.
        var isTestMessage = originalActorMessageId != null && originalActorMessageId.IsTestUuid();
        if (isTestMessage)
            return new ServiceBusSenderMock();

        return CreateEdiServiceBusSender();
    }

    private ServiceBusSender CreateEdiServiceBusSender()
    {
        return _serviceBusFactory.CreateClient(ServiceBusSenderNames.EdiTopic);
    }

    /// <summary>
    /// A service bus sender mock that does not send messages.
    /// </summary>
    private class ServiceBusSenderMock : ServiceBusSender
    {
        public override Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task SendMessagesAsync(IEnumerable<ServiceBusMessage> messages, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task SendMessagesAsync(
            ServiceBusMessageBatch messageBatch,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public override Task<long> ScheduleMessageAsync(
            ServiceBusMessage message,
            DateTimeOffset scheduledEnqueueTime,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(long.MinValue);
        }

        public override Task<IReadOnlyList<long>> ScheduleMessagesAsync(
            IEnumerable<ServiceBusMessage> messages,
            DateTimeOffset scheduledEnqueueTime,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult((IReadOnlyList<long>)[]);
        }
    }
}
