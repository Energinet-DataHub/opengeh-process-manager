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

using System.Reflection;
using Energinet.DataHub.ProcessManagement.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Application.Registration;
using Energinet.DataHub.ProcessManagement.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Scheduling;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>
/// that allow adding ProcessManager services to an application.
/// </summary>
public static class ProcessManagerExtensions
{
    /// <summary>
    /// Register options and services necessary for enabling an application to use the Process Manager
    /// to manage and monitor orchestrations.
    /// Should be used from the Process Manager API / Scheduler application.
    /// </summary>
    public static IServiceCollection AddProcessManagerCore(this IServiceCollection services)
    {
        // Process Manager Core
        services
            .AddProcessManagerOptions()
            .AddProcessManagerDatabase();

        // DurableClient connected to Task Hub
        services.AddTaskHubStorage();
        services
            .AddDurableClientFactory()
            .TryAddSingleton<IDurableClient>(sp =>
            {
                // IDurableClientFactory has a singleton lifecycle and caches clients
                var clientFactory = sp.GetRequiredService<IDurableClientFactory>();
                var processManagerOptions = sp.GetRequiredService<IOptions<ProcessManagerTaskHubOptions>>().Value;

                var durableClient = clientFactory.CreateClient(new DurableClientOptions
                {
                    ConnectionName = nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString),
                    TaskHub = processManagerOptions.ProcessManagerTaskHubName,
                    IsExternalClient = true,
                });

                return durableClient;
            });

        // ProcessManager components using interfaces to restrict access to functionality
        // => Scheduling
        services.TryAddScoped<IScheduledOrchestrationInstancesByInstantQuery, OrchestrationInstanceRepository>();
        services.TryAddScoped<IStartScheduledOrchestrationInstanceCommand, OrchestrationInstanceManager>();
        services.TryAddScoped<IRecurringOrchestrationQueries, RecurringOrchestrationQueries>();
        // => Cancellation (manager)
        services.TryAddScoped<ICancelScheduledOrchestrationInstanceCommand, OrchestrationInstanceManager>();
        // => Start instance (manager)
        services.TryAddScoped<IOrchestrationInstanceExecutor, DurableOrchestrationInstanceExecutor>();
        services.TryAddScoped<IOrchestrationRegisterQueries, OrchestrationRegister>();
        services.TryAddScoped<IOrchestrationInstanceRepository, OrchestrationInstanceRepository>();
        services.TryAddScoped<IStartOrchestrationInstanceCommands, OrchestrationInstanceManager>();
        // => Public queries
        services.TryAddScoped<IOrchestrationInstanceQueries, OrchestrationInstanceRepository>();

        return services;
    }

    /// <summary>
    /// Register options and services necessary for integrating Durable Functions orchestrations with the
    /// Process Manager functionality.
    /// Should be used from host's that contains Durable Functions orchestrations.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblyToScan">Specify the host assembly to scan for types implementing <see cref="IOrchestrationDescriptionBuilder"/>.</param>
    public static IServiceCollection AddProcessManagerForOrchestrations(this IServiceCollection services, Assembly assemblyToScan)
    {
        // Process Manager Core
        services
            .AddProcessManagerOptions()
            .AddProcessManagerDatabase();

        // Task Hub connected to Durable Functions
        services.AddTaskHubStorage();
        services
            .AddDurableClientFactory()
            .TryAddSingleton<IDurableClient>(sp =>
            {
                // IDurableClientFactory has a singleton lifecycle and caches clients
                var clientFactory = sp.GetRequiredService<IDurableClientFactory>();
                var processManagerOptions = sp.GetRequiredService<IOptions<ProcessManagerTaskHubOptions>>().Value;

                var durableClient = clientFactory.CreateClient(new DurableClientOptions
                {
                    ConnectionName = nameof(ProcessManagerTaskHubOptions.ProcessManagerStorageConnectionString),
                    TaskHub = processManagerOptions.ProcessManagerTaskHubName,
                    IsExternalClient = true,
                });

                return durableClient;
            });

        // ProcessManager components using interfaces to restrict access to functionality
        // => Orchestration Descriptions registration during startup
        services.AddOrchestrationDescriptionBuilders(assemblyToScan);
        services.TryAddTransient<IOrchestrationRegister, OrchestrationRegister>();
        // => Start instance (manager)
        services.TryAddScoped<IOrchestrationInstanceExecutor, DurableOrchestrationInstanceExecutor>();
        services.TryAddScoped<IOrchestrationRegisterQueries, OrchestrationRegister>();
        services.TryAddScoped<IOrchestrationInstanceRepository, OrchestrationInstanceRepository>();
        services.TryAddScoped<IStartOrchestrationInstanceCommands, OrchestrationInstanceManager>();
        // => Public queries
        services.TryAddScoped<IOrchestrationInstanceQueries, OrchestrationInstanceRepository>();
        // => Public progress repository
        services.TryAddScoped<IOrchestrationInstanceProgressRepository, OrchestrationInstanceRepository>();

        return services;
    }

    /// <summary>
    /// Register implementations of <see cref="IOrchestrationDescriptionBuilder"/> found in <paramref name="assemblyToScan"/>.
    /// </summary>
    private static IServiceCollection AddOrchestrationDescriptionBuilders(this IServiceCollection services, Assembly assemblyToScan)
    {
        var interfaceType = typeof(IOrchestrationDescriptionBuilder);

        var implementingTypes = assemblyToScan
            .GetTypes()
            .Where(type => type.IsClass && interfaceType.IsAssignableFrom(type))
            .ToList();

        foreach (var implementingType in implementingTypes)
        {
            services.AddTransient(interfaceType, implementingType);
        }

        return services;
    }

    /// <summary>
    /// Register hierarchical Process Manager options.
    /// </summary>
    private static IServiceCollection AddProcessManagerOptions(this IServiceCollection services)
    {
        services
            .AddOptions<ProcessManagerOptions>()
            .BindConfiguration(ProcessManagerOptions.SectionName)
            .ValidateDataAnnotations();

        return services;
    }

    /// <summary>
    /// Register Process Manager database and health checks.
    /// Depends on <see cref="ProcessManagerOptions"/>.
    /// </summary>
    private static IServiceCollection AddProcessManagerDatabase(this IServiceCollection services)
    {
        services
            .AddDbContext<ProcessManagerContext>((sp, optionsBuilder) =>
            {
                var processManagerOptions = sp.GetRequiredService<IOptions<ProcessManagerOptions>>().Value;

                optionsBuilder.UseSqlServer(processManagerOptions.SqlDatabaseConnectionString, providerOptionsBuilder =>
                {
                    providerOptionsBuilder.UseNodaTime();
                    providerOptionsBuilder.EnableRetryOnFailure();
                });
            });

        services
            .AddHealthChecks()
            .AddDbContextCheck<ProcessManagerContext>(name: "ProcesManagerDatabase");

        return services;
    }

    /// <summary>
    /// Register Task Hub storage options.
    /// </summary>
    private static IServiceCollection AddTaskHubStorage(this IServiceCollection services)
    {
        services
            .AddOptions<ProcessManagerTaskHubOptions>()
            .BindConfiguration(configSectionPath: string.Empty)
            .ValidateDataAnnotations();

        return services;
    }
}
