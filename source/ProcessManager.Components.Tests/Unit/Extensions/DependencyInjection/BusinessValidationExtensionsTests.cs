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
using Energinet.DataHub.ProcessManager.Components.Abstractions.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Components.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03_ActorRequestProcessExample.V1.BusinessValidation.ValidationRules;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Components.Tests.Unit.Extensions.DependencyInjection;

public class BusinessValidationExtensionsTests
{
    private readonly IServiceCollection _serviceCollection = new ServiceCollection();

    private readonly Assembly _orchestrationsExampleAssembly = typeof(Orchestration_Brs_X03_V1).Assembly;
    private readonly Assembly _orchestrationsExampleAbstractionsAssembly = typeof(ActorRequestProcessExampleInputV1).Assembly;

    private readonly List<Type> _businessValidatorTypes =
    [
        typeof(BusinessValidator<ActorRequestProcessExampleInputV1>),
    ];

    public BusinessValidationExtensionsTests()
    {
        _serviceCollection.AddLogging();
    }

    [Fact]
    public void Given_BusinessValidatorTypes_When_GettingBusinessValidatedDtoTypesFromAssembly_Then_EachBusinessValidatedDtoTypeShouldHaveABusinessValidatorType()
    {
        // Given
        // => Business validator types are registered in _businessValidatorTypes

        // When
        var businessValidatedDtoTypesFromAssembly = _orchestrationsExampleAbstractionsAssembly.GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false, IsGenericType: false }
                && t.IsAssignableTo(typeof(IBusinessValidatedDto)))
            .ToList();

        // Then
        using var assertionScope = new AssertionScope();
        foreach (var businessValidatedDtoType in businessValidatedDtoTypesFromAssembly)
        {
            var expectedValidatorType = typeof(BusinessValidator<>).MakeGenericType(businessValidatedDtoType);
            _businessValidatorTypes.Should().ContainEquivalentOf(expectedValidatorType);
        }
    }

    [Fact]
    public void Given_BusinessValidationAddedForExampleAssemblies_When_ResolvingExpectedValidatorTypes_Then_ValidatorsCanBeResolved()
    {
        // Given
        _serviceCollection.AddBusinessValidation(assembliesToScan: [_orchestrationsExampleAssembly, _orchestrationsExampleAbstractionsAssembly]);

        var services = _serviceCollection.BuildServiceProvider();

        // When
        var resolveActions = _businessValidatorTypes
            .Select<Type, Action>(businessValidatorType => () =>
            {
                services.GetRequiredService(businessValidatorType);
            });

        using var assertionScope = new AssertionScope();
        foreach (var resolveAction in resolveActions)
        {
            resolveAction.Should().NotThrow("because the expected business validator should be resolvable in the service provider");
        }
    }

    [Fact]
    public void Given_BusinessValidationRulesAddedForExampleAssemblies_When_ResolvingAllValidationRules_Then_ContainsExpectedRules()
    {
        // Given
        _serviceCollection.AddBusinessValidation(assembliesToScan: [_orchestrationsExampleAssembly, _orchestrationsExampleAbstractionsAssembly]);
        var services = _serviceCollection.BuildServiceProvider();

        List<Type> expectedValidationRuleTypes =
        [
            typeof(BusinessReasonValidationRule),
        ];

        // When
        var allResolvedValidationRules = new List<object?>();

        // => Get all business validation rules for each type implementing IBusinessValidatedDto
        var businessValidatedDtoTypes = _orchestrationsExampleAbstractionsAssembly.DefinedTypes
            .Where(t =>
                t is { IsClass: true, IsAbstract: false, IsGenericType: false }
                && t.IsAssignableTo(typeof(IBusinessValidatedDto)));
        foreach (var businessValidatedDtoType in businessValidatedDtoTypes)
        {
            var interfaceTypeForBusinessValidatedDtoType = typeof(IBusinessValidationRule<>).MakeGenericType(businessValidatedDtoType);
            var validationRulesForBusinessValidatedDtoType = services.GetServices(interfaceTypeForBusinessValidatedDtoType);
            allResolvedValidationRules.AddRange(validationRulesForBusinessValidatedDtoType);
        }

        // Then
        using var assertionScope = new AssertionScope();
        foreach (var expectedValidationRuleType in expectedValidationRuleTypes)
        {
            allResolvedValidationRules.Should().ContainSingle(o => o!.GetType() == expectedValidationRuleType);
        }
    }
}
