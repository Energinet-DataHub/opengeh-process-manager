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

using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.Common.Identity;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.Builder;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.DependencyInjection;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Startup;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.ActorRequestProcessExample.V1.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;

var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        // Common
        services.AddApplicationInsightsForIsolatedWorker("ProcessManager.Example");
        services.AddHealthChecksForIsolatedWorker();
        services.AddTokenCredentialProvider();
        services.AddNodaTimeForApplication();
        services.AddSubsystemAuthenticationForIsolatedWorker(context.Configuration);
        // => Feature management
        services
            .AddAzureAppConfiguration()
            .AddFeatureManagement();

        // => Auto register Orchestration Descriptions builders and custom handlers
        services.AddProcessManagerForOrchestrations(typeof(Program).Assembly, context.Configuration);

        // => Add EnqueueActorMessages client
        services.AddServiceBusClientForApplication(
            context.Configuration,
            sp => sp.GetRequiredService<TokenCredentialProvider>().Credential);
        services.AddEnqueueActorMessages();

        // Add BusinessValidation
        var orchestrationsExampleAssembly = typeof(Program).Assembly;
        var orchestrationsExampleAbstractionsAssembly = typeof(ActorRequestProcessExampleInputV1).Assembly;
        services.AddBusinessValidation([orchestrationsExampleAssembly, orchestrationsExampleAbstractionsAssembly]);

        // Time component
        services.AddTimeComponent();

        // DataHub Calendar
        services.AddDataHubCalendarComponent();
    })
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Feature management
        //  * Enables middleware that handles refresh from Azure App Configuration (except for DF Orchestration triggers)
        builder.UseAzureAppConfigurationForIsolatedWorker();

        // Http => Authorization
        builder.UseFunctionsAuthorization();
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

await host.SynchronizeWithOrchestrationRegisterAsync("ProcessManager.Example.Orchestrations").ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);

public partial class Program
{
}
