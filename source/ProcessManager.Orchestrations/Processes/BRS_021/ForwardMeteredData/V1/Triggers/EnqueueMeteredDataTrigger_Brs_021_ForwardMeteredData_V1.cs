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
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;

public class EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1> logger,
    TelemetryClient telemetryClient)
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly ILogger<EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1> _logger = logger;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    /// <summary>
    /// Enqueue Messages for BRS-021.
    /// </summary>
    [Function(nameof(EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1))]
    [ExponentialBackoffRetry(5, "00:00:01", "00:01:00")]
    public async Task Run(
        [EventHubTrigger(
            $"%{ProcessManagerEventHubOptions.SectionName}:{nameof(ProcessManagerEventHubOptions.EventHubName)}%",
            IsBatched = true,
            Connection = ProcessManagerEventHubOptions.SectionName)]
        EventData[] messages)
    {
        // Tracks structured telemetry data for Application Insights, including request details such as duration, success/failure, and dependencies.
        // Enables distributed tracing, allowing correlation of this request with related telemetry (e.g., dependencies, exceptions, custom metrics) in the same operation.
        // Automatically tracks metrics like request count, duration, and failure rate for RequestTelemetry.
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>(nameof(EnqueueMeteredDataTrigger_Brs_021_ForwardMeteredData_V1));
        try
        {
            var tasks = messages.Select(async message =>
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<EnqueueMeasurementsHandlerV1>();
                var brs021ForwardMeteredDataNotifyVersion = GetBrs021ForwardMeteredDataNotifyVersion(message);

                var instanceId = GetInstanceId(message, brs021ForwardMeteredDataNotifyVersion);

                _logger.LogInformation("Received notification from Measurements for Instance: {InstanceId}", instanceId);
                await handler.HandleAsync(instanceId).ConfigureAwait(false);
            });

            // Wait for all tasks to complete
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            throw;
        }
    }

    private static Guid GetInstanceId(
        EventData message,
        Brs021ForwardMeteredDataNotifyVersion brs021ForwardMeteredDataNotifyVersion)
    {
        var instanceId = brs021ForwardMeteredDataNotifyVersion.Version switch
        {
            "1" or "v1" => HandleV1(message.EventBody),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(Brs021ForwardMeteredDataNotifyVersion),
                actualValue: brs021ForwardMeteredDataNotifyVersion.Version,
                message: $"Unhandled {nameof(Brs021ForwardMeteredDataNotifyVersion)} version."),
        };
        return instanceId;
    }

    private static Brs021ForwardMeteredDataNotifyVersion GetBrs021ForwardMeteredDataNotifyVersion(EventData message)
    {
        var brs021ForwardMeteredDataNotifyVersion = Brs021ForwardMeteredDataNotifyVersion.Parser.ParseFrom(message.EventBody);

        if (brs021ForwardMeteredDataNotifyVersion is null)
            throw new InvalidOperationException($"Failed to deserialize message to {nameof(Brs021ForwardMeteredDataNotifyVersion)}.");
        return brs021ForwardMeteredDataNotifyVersion;
    }

    private static Guid HandleV1(BinaryData messageEventBody)
    {
        var notifyV1 = Brs021ForwardMeteredDataNotifyV1.Parser.ParseFrom(messageEventBody);

        if (notifyV1 is null)
            throw new InvalidOperationException($"Failed to deserialize message to {nameof(Brs021ForwardMeteredDataNotifyV1)}.");

        return Guid.Parse(notifyV1.OrchestrationInstanceId);
    }
}
