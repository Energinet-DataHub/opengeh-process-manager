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

using Energinet.DataHub.Core.Messaging.Communication.Extensions.Options;
using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.SendMeasurements.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Handlers;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.V1.Triggers;

public class TerminateTrigger_Brs_021_ForwardMeteredData_V1(
    TerminateSendMeasurementsHandlerV1 terminateSendMeasurementsHandlerV1,
    TelemetryClient telemetryClient)
{
    private readonly TerminateSendMeasurementsHandlerV1 _terminateSendMeasurementsHandlerV1 = terminateSendMeasurementsHandlerV1;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    /// <summary>
    /// Terminate a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(TerminateTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{Brs021SendMeasurementsTopicOptions.SectionName}:{nameof(Brs021SendMeasurementsTopicOptions.NotifyTopicName)}%",
            $"%{Brs021SendMeasurementsTopicOptions.SectionName}:{nameof(Brs021SendMeasurementsTopicOptions.NotifySubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        string message)
    {
        // Tracks structured telemetry data for Application Insights, including request details such as duration, success/failure, and dependencies.
        // Enables distributed tracing, allowing correlation of this request with related telemetry (e.g., dependencies, exceptions, custom metrics) in the same operation.
        // Automatically tracks metrics like request count, duration, and failure rate for RequestTelemetry.
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>(nameof(TerminateTrigger_Brs_021_ForwardMeteredData_V1));
        try
        {
            var notify = GetNotifyOrchestrationInstanceV1(message);

            var orchestrationInstanceId = new Core.Domain.OrchestrationInstance.OrchestrationInstanceId(Guid.Parse(notify.OrchestrationInstanceId));
            await _terminateSendMeasurementsHandlerV1.HandleAsync(orchestrationInstanceId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            throw;
        }
    }

    private static NotifyOrchestrationInstanceV1 GetNotifyOrchestrationInstanceV1(string message)
    {
        var notify = NotifyOrchestrationInstanceV1.Parser.ParseJson(message);
        if (notify is not { EventName: ForwardMeteredDataNotifyEventV1.OrchestrationInstanceEventName })
        {
            throw new InvalidOperationException("Failed to deserialize message");
        }

        return notify;
    }
}
