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
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_Example.Example.V1.Model;
using Energinet.DataHub.Example.Orchestrations.Processes.BRS_Example.Example.V1;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.Startup;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Telemetry;
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

        // => Orchestration Descriptions
        services.AddProcessManagerForOrchestrations(() =>
        {
            // TODO:
            // We could implement an interface for "description building" which could then be implemented besides the orchestration.
            // During DI we could then search for all these interface implementations and register them automatically.
            // This would ensure we didn't have to update Program.cs when we change orchestrations.
            var brs_023_027_v1 = CreateBrs_Example_V1Description();

            return [brs_023_027_v1];
        });

        // => Handlers
        services.AddScoped<SearchHandler_Brs_Example>();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext);
    })
    .Build();

await host.SynchronizeWithOrchestrationRegisterAsync("ProcessManager.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

OrchestrationDescription CreateBrs_Example_V1Description()
{
    var orchestrationDescriptionUniqueName = new Brs_Example_Example_V1();

    var description = new OrchestrationDescription(
        uniqueName: new OrchestrationDescriptionUniqueName(
            orchestrationDescriptionUniqueName.Name,
            orchestrationDescriptionUniqueName.Version),
        canBeScheduled: true,
        functionName: nameof(Orchestration_Brs_Example_Example_V1));

    description.ParameterDefinition.SetFromType<Input_Brs_Example_Example_V1>();

    description.AppendStepDescription("Beregning");
    description.AppendStepDescription(
        "Besked dannelse",
        canBeSkipped: true,
        skipReason: "Do not perform this step for an internal calculation.");

    return description;
}