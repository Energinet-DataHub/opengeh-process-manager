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
using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements;

public class StartTrigger_Brs_021_SendMeasurements(
    IStartOrchestrationInstanceFromMessageHandler handler,
    TelemetryClient telemetryClient)
{
    private readonly IStartOrchestrationInstanceFromMessageHandler _handler = handler;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    /// <summary>
    /// Start a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(StartTrigger_Brs_021_SendMeasurements))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{Brs021SendMeasurementsTopicOptions.SectionName}:{nameof(Brs021SendMeasurementsTopicOptions.StartTopicName)}%",
            $"%{Brs021SendMeasurementsTopicOptions.SectionName}:{nameof(Brs021SendMeasurementsTopicOptions.StartSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        // Tracks structured telemetry data for Application Insights, including request details such as duration, success/failure, and dependencies.
        // Enables distributed tracing, allowing correlation of this request with related telemetry (e.g., dependencies, exceptions, custom metrics) in the same operation.
        // Automatically tracks metrics like request count, duration, and failure rate for RequestTelemetry.
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>(nameof(StartTrigger_Brs_021_SendMeasurements));
        try
        {
            await _handler.HandleAsync(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            throw;
        }
    }
}
