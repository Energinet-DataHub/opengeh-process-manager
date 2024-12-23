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

using Azure.Identity;
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.Builder;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.Startup;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Telemetry;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddTransient<IConfiguration>(_ => context.Configuration);

        var azureCredential = new DefaultAzureCredential();

        // Common
        services.AddApplicationInsightsForIsolatedWorker(TelemetryConstants.SubsystemName);
        services.AddHealthChecksForIsolatedWorker();
        services.AddNodaTimeForApplication();

        // Databricks workspaces
        services.AddDatabricksJobs(DatabricksWorkspaceNames.Wholesale);
        services.AddDatabricksJobs(DatabricksWorkspaceNames.Measurements);

        // ProcessManager
        services.AddProcessManagerTopic(azureCredential);
        // => Orchestration Descriptions
        services.AddProcessManagerForOrchestrations(() =>
        {
            // TODO:
            // We could implement an interface for "description building" which could then be implemented besides the orchestration.
            // During DI we could then search for all these interface implementations and register them automatically.
            // This would ensure we didn't have to update Program.cs when we change orchestrations.
            var brs_021_ElectricalHeatingCalculation_v1 = CreateDescription_Brs_021_ElectricalHeatingCalculation_V1();
            var brs_021_ForwardMeteredData_v1 = CreateDescription_Brs_021_ForwardMeteredData_V1();
            var brs_023_027_v1 = CreateDescription_Brs_023_027_V1();
            var brs_026_v1 = CreateDescription_Brs_026_V1();

            return [
                brs_021_ElectricalHeatingCalculation_v1,
                brs_021_ForwardMeteredData_v1,
                brs_023_027_v1,
                brs_026_v1];
        });
        // => Handlers
        services.AddScoped<SearchCalculationHandler>();
        services.AddScoped<StartCalculationHandlerV1>();
        services.AddScoped<RequestCalculatedEnergyTimeSeriesHandlerV1>();
        services.AddScoped<StartForwardMeteredDataHandler>();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext);
    })
    .Build();

await host.SynchronizeWithOrchestrationRegisterAsync("ProcessManager.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

OrchestrationDescription CreateDescription_Brs_021_ElectricalHeatingCalculation_V1()
{
    var orchestrationDescriptionUniqueName = new Brs_021_ElectricalHeatingCalculation_V1();

    var description = new OrchestrationDescription(
        uniqueName: new OrchestrationDescriptionUniqueName(
            orchestrationDescriptionUniqueName.Name,
            orchestrationDescriptionUniqueName.Version),
        canBeScheduled: true,
        functionName: nameof(Orchestration_Brs_021_ElectricalHeatingCalculation_V1));

    // DISABLED for now because ElectricalHeatingJob is currently failing
    description.RecurringCronExpression = string.Empty;
    ////// Runs at 12:00 and 17:00 every day
    ////description.RecurringCronExpression = "0 12,17 * * *";

    foreach (var step in Orchestration_Brs_021_ElectricalHeatingCalculation_V1.Steps)
    {
        description.AppendStepDescription(step.Name);
    }

    return description;
}

OrchestrationDescription CreateDescription_Brs_021_ForwardMeteredData_V1()
{
    var orchestrationDescriptionUniqueName = new Brs_021_ForwardedMeteredData_V1();

    var description = new OrchestrationDescription(
        uniqueName: new OrchestrationDescriptionUniqueName(
            orchestrationDescriptionUniqueName.Name,
            orchestrationDescriptionUniqueName.Version),
        canBeScheduled: false,
        functionName: nameof(Orchestration_Brs_021_ForwardMeteredData_V1));

    description.ParameterDefinition.SetFromType<MeteredDataForMeasurementPointMessageInputV1>();
    description.AppendStepDescription("Asynkron validering");
    description.AppendStepDescription("Gemmer");
    description.AppendStepDescription("Finder modtagere");
    description.AppendStepDescription("Udsend beskeder");

    return description;
}

OrchestrationDescription CreateDescription_Brs_023_027_V1()
{
    var orchestrationDescriptionUniqueName = new Brs_023_027_V1();

    var description = new OrchestrationDescription(
        uniqueName: new OrchestrationDescriptionUniqueName(
            orchestrationDescriptionUniqueName.Name,
            orchestrationDescriptionUniqueName.Version),
        canBeScheduled: true,
        functionName: nameof(Orchestration_Brs_023_027_V1));

    description.ParameterDefinition.SetFromType<CalculationInputV1>();

    description.AppendStepDescription("Beregning");
    description.AppendStepDescription(
        "Besked dannelse",
        canBeSkipped: true,
        skipReason: "Do not perform this step for an internal calculation.");

    return description;
}

OrchestrationDescription CreateDescription_Brs_026_V1()
{
    var orchestrationDescriptionUniqueName = new Brs_026_V1();

    var description = new OrchestrationDescription(
        uniqueName: new OrchestrationDescriptionUniqueName(
            orchestrationDescriptionUniqueName.Name,
            orchestrationDescriptionUniqueName.Version),
        canBeScheduled: false,
        functionName: nameof(Orchestration_RequestCalculatedEnergyTimeSeries_V1));

    description.ParameterDefinition.SetFromType<RequestCalculatedEnergyTimeSeriesInputV1>();

    description.AppendStepDescription("Asynkron validering");
    description.AppendStepDescription("Udsend beskeder");

    return description;
}
