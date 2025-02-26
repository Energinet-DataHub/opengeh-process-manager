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
using Energinet.DataHub.Core.App.Common.Extensions.DependencyInjection;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.Builder;
using Energinet.DataHub.Core.App.FunctionApp.Extensions.DependencyInjection;
using Energinet.DataHub.Core.Messaging.Communication.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Telemetry;
using Energinet.DataHub.ProcessManager.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Scheduler;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Http => Authorization
        builder.UseFunctionsAuthorization();
    })
    .ConfigureServices((context, services) =>
    {
        var azureCredential = new DefaultAzureCredential();

        // Common
        services.AddApplicationInsightsForIsolatedWorker(TelemetryConstants.SubsystemName);
        services.AddHealthChecksForIsolatedWorker();
        services.AddNodaTimeForApplication();
        services.AddServiceBusClientForApplication(context.Configuration);

        // Api
        services.AddNotifyOrchestrationInstance(azureCredential);

        // ProcessManager
        services.AddProcessManagerCore();
        services.AddProcessManagerAuthentication(context.Configuration); // TODO: PoC => Most likely merge with "AddProcessManagerCore"

        // Handlers
        services.AddScoped<RecurringPlannerHandler>();
        services.AddScoped<SchedulerHandler>();
    })
    .ConfigureLogging((hostingContext, logging) =>
    {
        logging.AddLoggingConfigurationForIsolatedWorker(hostingContext);
    })
    .Build();

host.Run();
