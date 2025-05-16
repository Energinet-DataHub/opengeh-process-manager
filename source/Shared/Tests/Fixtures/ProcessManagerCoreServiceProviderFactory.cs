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
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;

public static class ProcessManagerCoreServiceProviderFactory
{
    /// <summary>
    /// Register common services and Process Manager services necessary to
    /// test classes depending on e.g. <see cref="IOrchestrationInstanceExecutor"/>.
    /// </summary>
    /// <param name="databaseConnectionString"></param>
    /// <param name="configureMockedServices">Use this action to register services we want to mock, as they must be registered first.</param>
    /// <param name="configureServices">Use this action to register additional services, that is not part of those we always register.</param>
    public static ServiceProvider BuildServiceProvider(
        string databaseConnectionString,
        Action<IServiceCollection> configureMockedServices,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();

        // Services we want to mock MUST be registered before we call Process Manager DI extensions because we always use "TryAdd" within those
        configureMockedServices(services);

        // Common
        services.AddLogging();
        services.AddNodaTimeForApplication();
        services.AddFeatureManagement();

        // App settings
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProcessManagerOptions.SectionName}:{nameof(ProcessManagerOptions.SqlDatabaseConnectionString)}"]
                        = databaseConnectionString,
                [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString)}"]
                        = "Not used, but cannot be empty",
                [$"{nameof(ProcessManagerTaskHubOptions.ProcessManagerTaskHubName)}"]
                        = "Not used, but cannot be empty",
            }).Build();

        // Process Manager
        services.AddScoped<IConfiguration>(_ => configuration);
        services.AddProcessManagerCore();
        // => Additional registration to ensure we can keep the database consistent by adding orchestration descriptions
        services.AddTransient<IOrchestrationRegister, OrchestrationRegister>();

        // Register any additional service for the specific test
        if (configureServices != null)
            configureServices(services);

        return services.BuildServiceProvider();
    }
}
