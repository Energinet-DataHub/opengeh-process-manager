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
using Energinet.DataHub.ProcessManager.Abstractions.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.GridAreaOwner;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;

public static class BusinessValidationExtensions
{
    /// <summary>
    /// Add required services for business validation. Registers implementations of <see cref="BusinessValidator{TInput}"/>
    /// and <see cref="IBusinessValidationRule{TInput}"/> from the given <paramref name="assembliesToScan"/>.
    /// <remarks>
    /// <see cref="BusinessValidator{TInput}"/> implementations are registered by finding <see cref="IBusinessValidatedDto"/>
    /// types in the given <paramref name="assembliesToScan"/>, and registering a <see cref="BusinessValidator{TInput}"/>
    /// for each type found. This means the given <paramref name="assembliesToScan"/> must also include the assembly that
    /// contains the types that implement <see cref="IBusinessValidatedDto"/>
    /// (example: ProcessManager.Orchestrations and ProcessManager.Orchestrations.Abstractions assemblies).
    /// </remarks>
    /// </summary>
    public static IServiceCollection AddBusinessValidation(
        this IServiceCollection services,
        IReadOnlyCollection<Assembly> assembliesToScan)
    {
        services.AddBusinessValidatorImplementations(assembliesToScan);
        services.AddBusinessValidationRuleImplementations(assembliesToScan);

        services.AddTransient<PeriodValidationHelper>();

        // TODO: Replace GridAreaOwnerMockClient with actual client
        services.AddTransient<IGridAreaOwnerClient, GridAreaOwnerMockClient>();

        return services;
    }

    /// <summary>
    /// Register implementations of <see cref="BusinessValidator{TInput}"/> for
    /// each <see cref="IBusinessValidatedDto"/> type found in <paramref name="assemblies"/>.
    /// </summary>
    private static IServiceCollection AddBusinessValidatorImplementations(
        this IServiceCollection services,
        IReadOnlyCollection<Assembly> assemblies)
    {
        var businessValidatedDtoTypes = assemblies
            .SelectMany(a => a.DefinedTypes).Distinct()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false, IsGenericType: false }
                && t.IsAssignableTo(typeof(IBusinessValidatedDto)));

        foreach (var businessValidatedDtoType in businessValidatedDtoTypes)
        {
            var businessValidatorTypeForDtoType = typeof(BusinessValidator<>).MakeGenericType(businessValidatedDtoType);
            services.AddTransient(businessValidatorTypeForDtoType);
        }

        return services;
    }

    /// <summary>
    /// Register implementations of <see cref="IBusinessValidationRule{TInput}"/> found in <paramref name="assemblies"/>.
    /// </summary>
    private static IServiceCollection AddBusinessValidationRuleImplementations(
        this IServiceCollection services,
        IReadOnlyCollection<Assembly> assemblies)
    {
        var validationRuleInterfaceType = typeof(IBusinessValidationRule<>);

        var validationRuleImplementationTypes = assemblies
            .SelectMany(a => a.DefinedTypes).Distinct()
            .Where(typeInfo =>
                typeInfo is { IsClass: true, IsAbstract: false }
                && typeInfo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == validationRuleInterfaceType))
            .ToList();

        foreach (var validationRuleType in validationRuleImplementationTypes)
        {
            var interfaceTypeForValidationRule = validationRuleType.GetInterfaces()
                .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == validationRuleInterfaceType);
            services.AddTransient(interfaceTypeForValidationRule, validationRuleType);
        }

        return services;
    }
}
