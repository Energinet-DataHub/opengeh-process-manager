﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.Builder;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.Startup;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Telemetry;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Common
        services.AddApplicationInsightsForIsolatedWorker(TelemetryConstants.SubsystemName);
        services.AddHealthChecksForIsolatedWorker();
        services.AddNodaTimeForApplication();

        // ProcessManager
        services.AddProcessManagerTopic();
        // => Orchestration Descriptions
        services.AddProcessManagerForOrchestrations(() =>
        {
            // TODO: For demo purposes; remove when done.
            // We could implement an interface for "description building" which could then be implemented besides the orchestration.
            // During DI we could then search for all these interface implementations and register them automatically.
            // This would ensure we didn't have to update Program.cs when we change orchestrations.
            var brs_023_027_v1 = CreateBRS_023_027_V1Description();
            var brs_026_v1 = CreateBRS_026_V1Description();

            return [brs_023_027_v1, brs_026_v1];
        });
        // => Handlers
        services.AddScoped<NotifyAggregatedMeasureDataHandler>();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext);
    })
    .Build();

await host.SynchronizeWithOrchestrationRegisterAsync("ProcessManager.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

OrchestrationDescription CreateBRS_023_027_V1Description()
{
    var brs023027V1 = new OrchestrationDescription(
        name: "BRS_023_027",
        version: 1,
        canBeScheduled: true,
        functionName: nameof(NotifyAggregatedMeasureDataOrchestrationV1));

    brs023027V1.ParameterDefinition.SetFromType<NotifyAggregatedMeasureDataInputV1>();

    brs023027V1.AppendStepDescription("Beregning");
    brs023027V1.AppendStepDescription(
        "Besked dannelse",
        canBeSkipped: true,
        skipReason: "Do not perform this step for an internal calculation.");
    return brs023027V1;
}

OrchestrationDescription CreateBRS_026_V1Description()
{
    var brs026V1 = new OrchestrationDescription(
        name: "BRS_026",
        version: 1,
        canBeScheduled: false,
        functionName: nameof(RequestCalculatedEnergyTimeSeriesOrchestrationV1));
    brs026V1.ParameterDefinition.SetFromType<RequestCalculatedEnergyTimeSeriesInputV1>();
    brs026V1.AppendStepDescription("Asynkron validering");
    brs026V1.AppendStepDescription("Hent anmodningsdata");
    brs026V1.AppendStepDescription("Udsend beskeder");
    return brs026V1;
}