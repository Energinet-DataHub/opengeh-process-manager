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
using Energinet.DataHub.ProcessManager.Core.Application;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Application.Scheduling;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Diagnostics.HealthChecks;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Registration;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Scheduling;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.ContextImplementations;
using Microsoft.Azure.WebJobs.Extensions.DurableTask.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;

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
            .AddProcessManagerDatabase()
            .AddFeatureFlags();

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
        services.TryAddScoped<IStartOrchestrationInstanceMessageCommands, OrchestrationInstanceManager>();
        // => Notify instance (manager)
        services.TryAddScoped<INotifyOrchestrationInstanceCommands, OrchestrationInstanceManager>();
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
            .AddProcessManagerDatabase()
            .AddFeatureFlags();

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
        services.TryAddScoped<IStartOrchestrationInstanceMessageCommands, OrchestrationInstanceManager>();
        // => Public queries
        services.TryAddScoped<IOrchestrationInstanceQueries, OrchestrationInstanceRepository>();
        // => Public progress repository
        services.TryAddScoped<IOrchestrationInstanceProgressRepository, OrchestrationInstanceRepository>();
        // => Custom handlers
        services.AddCustomHandlersForHttpTriggers(assemblyToScan);
        services.AddCustomHandlersForServiceBusTriggers(assemblyToScan);
        // => Add custom dependencies
        services.AddCustomOptions(assemblyToScan);

        // => Orchestration register healthcheck
        services.AddSingleton<OrchestrationRegisterContext>();
        services.AddHealthChecks()
            .AddTypeActivatedCheck<OrchestrationRegisterHealthCheck>("Orchestration register", HealthStatus.Unhealthy);

        // => For the feature Migrate Wholesale Calculations
        services.TryAddTransient<IOrchestrationInstanceFactory, OrchestrationInstanceFactory>();

        return services;
    }

    /// <summary>
    /// Register implementations of <see cref="IOrchestrationDescriptionBuilder"/> found in <paramref name="assemblyToScan"/>.
    /// </summary>
    internal static IServiceCollection AddOrchestrationDescriptionBuilders(this IServiceCollection services, Assembly assemblyToScan)
    {
        var interfaceType = typeof(IOrchestrationDescriptionBuilder);

        var implementingTypes = assemblyToScan
            .DefinedTypes
            .Where(typeInfo =>
                typeInfo.IsClass
                && !typeInfo.IsAbstract
                && interfaceType.IsAssignableFrom(typeInfo))
            .ToList();

        foreach (var implementingType in implementingTypes)
        {
            services.AddTransient(interfaceType, implementingType);
        }

        return services;
    }

    /// <summary>
    /// Register implementations of various custom handler used from HTTP triggers found in <paramref name="assemblyToScan"/>.
    /// </summary>
    internal static IServiceCollection AddCustomHandlersForHttpTriggers(this IServiceCollection services, Assembly assemblyToScan)
    {
        var handlerInterfaces = new List<Type>
        {
            typeof(IStartOrchestrationInstanceCommandHandler<,>),
            typeof(IScheduleOrchestrationInstanceCommandHandler<,>),
            typeof(ISearchOrchestrationInstancesQueryHandler<,>),
        };

        foreach (var handlerInterface in handlerInterfaces)
        {
            var implementingTypes = assemblyToScan
                .DefinedTypes
                .Where(typeInfo =>
                    typeInfo.IsClass &&
                    !typeInfo.IsAbstract &&
                    typeInfo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
                .ToList();

            foreach (var implementingType in implementingTypes)
            {
                // We register handlers directly by their implementation, not by an interface.
                // We DO NOT register the same type twice; e.g. if the same class implements two interfaces, we only register it once.
                services.TryAddTransient(implementingType);
            }
        }

        return services;
    }

    /// <summary>
    /// Register implementations of various custom handler used from ServiceBus triggers found in <paramref name="assemblyToScan"/>.
    /// </summary>
    internal static IServiceCollection AddCustomHandlersForServiceBusTriggers(this IServiceCollection services, Assembly assemblyToScan)
    {
        var handlerBaseType = typeof(StartOrchestrationInstanceFromMessageHandlerBase<>);

        var implementingTypes = assemblyToScan
            .DefinedTypes
            .Where(typeInfo =>
                typeInfo.IsClass &&
                !typeInfo.IsAbstract &&
                IsDirectSubclassOfHandlerBaseType(typeInfo, handlerBaseType))
            .ToList();

        foreach (var implementingType in implementingTypes)
        {
            services.AddTransient(implementingType);
        }

        return services;
    }

    /// <summary>
    /// Finds all implementations of "IOptionsConfiguration" in <paramref name="assemblyToScan"/>.
    /// Then registries everything in the "Configure" method.
    /// This should only be used to add options.
    /// </summary>
    internal static IServiceCollection AddCustomOptions(this IServiceCollection services, Assembly assemblyToScan)
    {
        var interfaceType = typeof(IOptionsConfiguration);

        var implementingTypes = assemblyToScan.DefinedTypes
            .Where(typeInfo =>
                typeInfo.IsClass &&
                !typeInfo.IsAbstract &&
                typeInfo.IsAssignableTo(interfaceType))
            .ToList();

        var serviceAdders = implementingTypes
                .Select(Activator.CreateInstance)
                .Where(instance => instance != null)
                .Select(instance => (IOptionsConfiguration)instance!);

        foreach (var adder in serviceAdders)
        {
            adder.Configure(services);
        }

        return services;
    }

    private static bool IsDirectSubclassOfHandlerBaseType(TypeInfo typeInfo, Type handlerBaseType)
    {
        return
            typeInfo.BaseType != null
            && typeInfo.BaseType.IsGenericType
            && typeInfo.BaseType.GetGenericTypeDefinition() == handlerBaseType;
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
            .AddDbContextCheck<ProcessManagerContext>(name: "ProcessManagerDatabase");

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
