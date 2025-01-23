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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class BusinessValidationExtensions
{
    /// <summary>
    /// Add required services for business validation. Registers implementations of <see cref="BusinessValidatorBase{TInput}"/>
    /// and <see cref="IBusinessValidationRule{TInput}"/> found in the given <paramref name="assemblyToScan"/>.
    /// </summary>
    public static IServiceCollection AddBusinessValidation(this IServiceCollection services, Assembly assemblyToScan)
    {
        services.AddBusinessValidatorImplementations(assemblyToScan);
        services.AddBusinessValidationRuleImplementations(assemblyToScan);

        // TODO: Replace GridAreaOwnerMockClient with actual client
        services.AddTransient<IGridAreaOwnerClient, GridAreaOwnerMockClient>();

        return services;
    }

    /// <summary>
    /// Register implementations of <see cref="BusinessValidatorBase{TInput}"/> found in <paramref name="assemblyToScan"/>.
    /// </summary>
    private static IServiceCollection AddBusinessValidatorImplementations(this IServiceCollection services, Assembly assemblyToScan)
    {
        var baseType = typeof(BusinessValidatorBase<>);

        var implementingTypes = assemblyToScan
            .DefinedTypes
            .Where(typeInfo =>
                typeInfo is { IsClass: true, IsAbstract: false }
                && baseType.IsAssignableFrom(typeInfo))
            .ToList();

        foreach (var implementingType in implementingTypes)
        {
            services.AddTransient(implementingType);
        }

        return services;
    }

    /// <summary>
    /// Register implementations of <see cref="IBusinessValidationRule{TInput}"/> found in <paramref name="assemblyToScan"/>.
    /// </summary>
    private static IServiceCollection AddBusinessValidationRuleImplementations(this IServiceCollection services, Assembly assemblyToScan)
    {
        var interfaceType = typeof(IBusinessValidationRule<>);

        var implementingTypes = assemblyToScan
            .DefinedTypes
            .Where(typeInfo =>
                typeInfo is { IsClass: true, IsAbstract: false }
                && interfaceType.IsAssignableFrom(typeInfo))
            .ToList();

        foreach (var implementingType in implementingTypes)
        {
            services.AddTransient(interfaceType, implementingType);
        }

        return services;
    }
}
