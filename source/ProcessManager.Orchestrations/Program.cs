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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
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
    .ConfigureAppConfiguration((context, config) =>
    {
        context.AddConfiguration(config);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddTransient<IConfiguration>(_ => context.Configuration);

        var azureCredential = new DefaultAzureCredential();

        // Common
        services.AddApplicationInsightsForIsolatedWorker(TelemetryConstants.SubsystemName);
        services.AddHealthChecksForIsolatedWorker();
        services.AddNodaTimeForApplication();
        services.AddServiceBusClientForApplication(context.Configuration);

        // Databricks Workspaces
        services.AddDatabricksJobs(DatabricksWorkspaceNames.Wholesale);
        services.AddDatabricksJobs(DatabricksWorkspaceNames.Measurements);

        // Enqueue Messages in EDI
        services.AddEnqueueActorMessages(azureCredential);

        // Integration event publisher
        services.AddIntegrationEventPublisher(azureCredential);

        // Business validation
        var orchestrationsAssembly = typeof(Program).Assembly;
        var orchestrationsAbstractionsAssembly = typeof(RequestCalculatedEnergyTimeSeriesInputV1).Assembly;
        services.AddBusinessValidation([orchestrationsAssembly, orchestrationsAbstractionsAssembly]);

        // Time component
        services.AddTimeComponent();

        // Azure App Configuration
        services.AddAzureAppConfiguration();

        // ProcessManager
        services.AddProcessManagerTopic(azureCredential);
        // => Auto register Orchestration Descriptions builders and custom handlers
        services.AddProcessManagerForOrchestrations(context.Configuration, typeof(Program).Assembly);

        // BRS-021 ForwardMeteredData
        services.AddBrs021ForwardMeteringData(azureCredential);
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext);
    })
    .Build();

await host.SynchronizeWithOrchestrationRegisterAsync("ProcessManager.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

public partial class Program
{
}
