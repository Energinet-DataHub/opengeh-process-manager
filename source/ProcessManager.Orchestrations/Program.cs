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

using Azure.Identity;
using DurableFunctionsMonitor.DotNetIsolated;
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.Builder;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.DependencyInjection;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Startup;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Telemetry;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddTransient<IConfiguration>(_ => context.Configuration);

        var azureCredential = new DefaultAzureCredential();

        // Common
        services.AddApplicationInsightsForIsolatedWorker(TelemetryConstants.SubsystemName);
        services.AddHealthChecksForIsolatedWorker();
        services.AddNodaTimeForApplication();
        services.AddServiceBusClientForApplication(context.Configuration);
        // => Feature management
        services
            .AddAzureAppConfiguration()
            .AddFeatureManagement();

        // Databricks
        services.AddDatabricksJobsApi(DatabricksWorkspaceNames.Wholesale);
        services.AddDatabricksJobsApi(DatabricksWorkspaceNames.Measurements);
        services.AddDatabricksSqlStatementApi(context.Configuration);

        // Enqueue Messages in EDI
        services.AddEnqueueActorMessages(azureCredential);
        services.AddEnqueueActorMessagesHttp(azureCredential, context.Configuration);

        // Integration event publisher
        services.AddIntegrationEventPublisher(azureCredential);

        // Business validation
        var orchestrationsAssembly = typeof(Program).Assembly;
        var orchestrationsAbstractionsAssembly = typeof(RequestCalculatedEnergyTimeSeriesInputV1).Assembly;
        services.AddBusinessValidation([orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        // Time component
        services.AddTimeComponent();

        // DataHub Calendar
        services.AddDataHubCalendarComponent();

        // ProcessManager
        services.AddProcessManagerTopic(azureCredential);
        // => Auto register Orchestration Descriptions builders and custom handlers
        services.AddProcessManagerForOrchestrations(context.Configuration, typeof(Program).Assembly);

        // BRS-021 (ForwardMeteredData, ElectricalHeatingCalculation, CapacitySettlementCalculation & NetConsumptionCalculation)
        services.AddBrs021(azureCredential);
    })
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Feature management
        //  * Enables middleware that handles refresh from Azure App Configuration (except for DF Orchestration triggers)
        builder.UseAzureAppConfigurationForIsolatedWorker();

        // Http => Authorization
        builder.UseFunctionsAuthorization();

        // Host Durable Function Monitor as a part of this app.
        // The Durable Function Monitor can be accessed at: {host url}/api/durable-functions-monitor
        builder.UseDurableFunctionsMonitor(
            (settings, _) =>
            {
                settings.Mode = DfmMode.ReadOnly;
            });
    })
    .ConfigureAppConfiguration((context, configBuilder) =>
    {
        // Feature management
        //  * Configure load/refresh from Azure App Configuration
        configBuilder.AddAzureAppConfigurationForIsolatedWorker();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext.Configuration);
    })
    .Build();

await host.SynchronizeWithOrchestrationRegisterAsync("ProcessManager.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

public partial class Program
{
}
