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
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Triggers;

public class StartTrigger_Brs_021_ForwardMeteredData_V1(
    StartForwardMeteredDataHandlerV1 handler,
    TelemetryClient telemetryClient)
{
    private readonly StartForwardMeteredDataHandlerV1 _handler = handler;
    private readonly TelemetryClient _telemetryClient = telemetryClient;

    /// <summary>
    /// Start a BRS-021 ForwardMeteredData.
    /// </summary>
    [Function(nameof(StartTrigger_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ServiceBusTrigger(
            $"%{Brs021ForwardMeteredDataTopicOptions.SectionName}:{nameof(Brs021ForwardMeteredDataTopicOptions.StartTopicName)}%",
            $"%{Brs021ForwardMeteredDataTopicOptions.SectionName}:{nameof(Brs021ForwardMeteredDataTopicOptions.StartSubscriptionName)}%",
            Connection = ServiceBusNamespaceOptions.SectionName)]
        ServiceBusReceivedMessage message)
    {
        // Provides structured telemetry data that Application Insights uses to display request details,
        // including duration, success/failure, and associated dependencies.
        // Distributed Tracing: Enables distributed tracing, allowing correlation of this request
        // with other telemetry (e.g., dependencies, exceptions) in the same operation.
        // Automatic Metrics: Automatically tracks metrics like request count, duration, and failure rate for RequestTelemetry.
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>(nameof(StartTrigger_Brs_021_ForwardMeteredData_V1));
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
