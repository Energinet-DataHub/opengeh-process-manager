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
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1.Model;
using Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.Example;
using Energinet.DataHub.Example.Orchestrations.Processes.BRS_X01.Example.V1;
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
            var brs_X01_Example_V1 = CreateBrs_X01_Example_V1Description();

            return [brs_X01_Example_V1];
        });

        // => Handlers
        services.AddScoped<SearchExampleHandler>();
        services.AddScoped<StartExampleHandlerV1>();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext);
    })
    .Build();

await host.SynchronizeWithOrchestrationRegisterAsync("Example.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

OrchestrationDescription CreateBrs_X01_Example_V1Description()
{
    var orchestrationDescriptionUniqueName = new Brs_X01_Example_V1();

    var description = new OrchestrationDescription(
        uniqueName: new OrchestrationDescriptionUniqueName(
            orchestrationDescriptionUniqueName.Name,
            orchestrationDescriptionUniqueName.Version),
        canBeScheduled: true,
        functionName: nameof(Orchestration_Brs_X01_Example_V1));

    description.ParameterDefinition.SetFromType<InputV1>();

    description.AppendStepDescription("Example step 1");
    description.AppendStepDescription(
        "Example step 2, can be skipped",
        canBeSkipped: true,
        skipReason: "Do not perform this step if skipped");
    description.AppendStepDescription("Example step 3");

    return description;
}