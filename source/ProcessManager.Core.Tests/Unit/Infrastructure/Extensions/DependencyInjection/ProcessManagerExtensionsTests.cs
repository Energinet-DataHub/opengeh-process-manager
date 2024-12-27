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
using Energinet.DataHub.ProcessManagement.Core.Application.Registration;
using Energinet.DataHub.ProcessManagement.Core.Infrastructure.Extensions.DependencyInjection;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Infrastructure.Extensions.DependencyInjection;

public class ProcessManagerExtensionsTests
{
    public ProcessManagerExtensionsTests()
    {
        ExampleOrchestrationsAssembly = Assembly.GetAssembly(typeof(Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Orchestration_Brs_X01_NoInputExample_V1))!;
        Services = new ServiceCollection();
    }

    public Assembly ExampleOrchestrationsAssembly { get; }

    private ServiceCollection Services { get; }

    [Fact]
    public void AddOrchestrationDescriptionBuilders_WhenScanningExampleOrchestrations_ExpectedBuildersAreRegistered()
    {
        // Arrange
        var expectedBuilderTypes = new List<Type>
        {
            typeof(Example.Orchestrations.Processes.BRS_X01.InputExample.V1.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.OrchestrationDescriptionBuilder),
        };

        // Act
        Services.AddOrchestrationDescriptionBuilders(assemblyToScan: ExampleOrchestrationsAssembly);

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        var actualBuilders = serviceProvider.GetServices<IOrchestrationDescriptionBuilder>();
        actualBuilders.Select(x => x.GetType())
            .Should().Contain(expectedBuilderTypes, "because all expected builder types should have been found");

        actualBuilders.Count()
            .Should().Be(expectedBuilderTypes.Count, "because we should find exactly the number of expected builder types");
    }
}
