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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Contracts;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.Triggers;

public class EnqueueMeasurementsTrigger_Brs_021_ForwardMeasurements_V1(
    EnqueueMeasurementsHandlerV1 handler,
    ILogger<EnqueueMeasurementsTrigger_Brs_021_ForwardMeasurements_V1> logger,
    TelemetryClient telemetryClient)
{
    private readonly EnqueueMeasurementsHandlerV1 _handler = handler;
    private readonly ILogger<EnqueueMeasurementsTrigger_Brs_021_ForwardMeasurements_V1> _logger = logger;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    /// <summary>
    /// Enqueue Messages for BRS-021.
    /// </summary>
    [Function(nameof(EnqueueMeasurementsTrigger_Brs_021_ForwardMeasurements_V1))]
    [ExponentialBackoffRetry(5, "00:00:01", "00:01:00")]
    public async Task Run(
        [EventHubTrigger(
            $"%{ProcessManagerEventHubOptions.SectionName}:{nameof(ProcessManagerEventHubOptions.EventHubName)}%",
            IsBatched = false,
            Connection = ProcessManagerEventHubOptions.SectionName)]
        EventData message)
    {
        // Tracks structured telemetry data for Application Insights, including request details such as duration, success/failure, and dependencies.
        // Enables distributed tracing, allowing correlation of this request with related telemetry (e.g., dependencies, exceptions, custom metrics) in the same operation.
        // Automatically tracks metrics like request count, duration, and failure rate for RequestTelemetry.
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>(nameof(EnqueueMeasurementsTrigger_Brs_021_ForwardMeasurements_V1));
        try
        {
            var notifyVersion = GetBrs021ForwardMeasurementsNotifyVersion(message);

            var orchestrationInstanceId = GetOrchestrationInstanceId(message, notifyVersion);

            _logger.LogInformation("Received notification from Measurements for Orchestration Instance: {OrchestrationInstanceId}", orchestrationInstanceId);
            await _handler.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            throw;
        }
    }

    private static OrchestrationInstanceId GetOrchestrationInstanceId(
        EventData message,
        Brs021ForwardMeteredDataNotifyVersion notifyVersion)
    {
        var orchestrationInstanceId = notifyVersion.Version switch
        {
            "1" or "v1" => HandleV1(message.EventBody),
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(Brs021ForwardMeteredDataNotifyVersion),
                actualValue: notifyVersion.Version,
                message: $"Unhandled {nameof(Brs021ForwardMeteredDataNotifyVersion)} version."),
        };
        return orchestrationInstanceId;
    }

    private static Brs021ForwardMeteredDataNotifyVersion GetBrs021ForwardMeasurementsNotifyVersion(EventData message)
    {
        var notifyVersion = Brs021ForwardMeteredDataNotifyVersion.Parser.ParseFrom(message.EventBody);

        if (notifyVersion is null)
            throw new InvalidOperationException($"Failed to deserialize message to {nameof(Brs021ForwardMeteredDataNotifyVersion)}.");

        return notifyVersion;
    }

    private static OrchestrationInstanceId HandleV1(BinaryData messageEventBody)
    {
        var notifyV1 = Brs021ForwardMeteredDataNotifyV1.Parser.ParseFrom(messageEventBody);

        if (notifyV1 is null)
            throw new InvalidOperationException($"Failed to deserialize message to {nameof(Brs021ForwardMeteredDataNotifyV1)}.");

        return new OrchestrationInstanceId(Guid.Parse(notifyV1.OrchestrationInstanceId));
    }
}
