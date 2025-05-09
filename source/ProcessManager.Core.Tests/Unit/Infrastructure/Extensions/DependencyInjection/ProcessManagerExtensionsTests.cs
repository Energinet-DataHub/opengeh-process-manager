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
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Options;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Energinet.DataHub.ProcessManager.Core.Tests.Unit.Infrastructure.Extensions.DependencyInjection;

public class ProcessManagerExtensionsTests
{
    public ProcessManagerExtensionsTests()
    {
        ExampleOrchestrationsAssembly = Assembly.GetAssembly(typeof(OrchestrationOptions_Brs_X01_InputExample_V1))!;
        Services = new ServiceCollection();
    }

    private Assembly ExampleOrchestrationsAssembly { get; }

    private ServiceCollection Services { get; }

    [Fact]
    public void AddOrchestrationDescriptionBuilders_WhenScanningExampleOrchestrations_ExpectedBuildersAreRegistered()
    {
        // Arrange
        var expectedBuilderTypes = new List<Type>
        {
            typeof(Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Orchestration.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X01.InputExample.V1.Orchestration.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Orchestration.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.Orchestration.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Orchestration.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X03.FailingOrchestrationInstanceExample.V1.Orchestration.OrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X03.OrchestrationDescriptionBreakingChanges.BreakingChangesOrchestrationDescriptionBuilder),
            typeof(Example.Orchestrations.Processes.BRS_X03.OrchestrationDescriptionBreakingChanges.UnderDevelopmentOrchestrationDescriptionBuilder),
        };

        // Act
        Services.AddOrchestrationDescriptionBuilders(assemblyToScan: ExampleOrchestrationsAssembly);

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        var actualBuilders = serviceProvider.GetServices<IOrchestrationDescriptionBuilder>().ToList();
        actualBuilders.Select(x => x.GetType())
            .Should().Contain(expectedBuilderTypes, "because all expected builder types should have been found");

        actualBuilders.Count()
            .Should().Be(expectedBuilderTypes.Count, "because we should find exactly the number of expected builder types");
    }

    [Fact]
    public void AddCustomHandlersForHttpTriggers_WhenScanningExampleOrchestrations_ExpectedHandlersAreRegistered()
    {
        // Arrange
        Services.AddSingleton(Mock.Of<IStartOrchestrationInstanceCommands>());
        Services.AddSingleton(new ProcessManagerReaderContext(new Microsoft.EntityFrameworkCore.DbContextOptions<ProcessManagerReaderContext>()));

        // Act
        Services.AddCustomHandlersForHttpTriggers(assemblyToScan: ExampleOrchestrationsAssembly);

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // => Start handler
        var actualStartHandler = serviceProvider.GetRequiredService<Example.Orchestrations.Processes.BRS_X01.InputExample.V1.StartInputExampleHandlerV1>();
        actualStartHandler.Should().NotBeNull();

        // => Search handler for list
        var actualSearchPluralHandler = serviceProvider.GetRequiredService<Example.Orchestrations.CustomQueries.Examples.V1.SearchExamplesHandlerV1>();
        actualSearchPluralHandler.Should().NotBeNull();

        // => Search handler for id
        var actualSearchSingleHandler = serviceProvider.GetRequiredService<Example.Orchestrations.CustomQueries.Examples.V1.SearchExampleByIdHandlerV1>();
        actualSearchSingleHandler.Should().NotBeNull();
    }

    [Fact]
    public void AddCustomOptions_WhenScanningExampleOrchestrations_CollectionContainsExpectedServices()
    {
        // Arrange
        const string expectedOptionValue = "not-empty-string";
        Services.AddInMemoryConfiguration(new Dictionary<string, string?>()
        {
            {
                $"{OrchestrationOptions_Brs_X01_InputExample_V1.SectionName}:{nameof(OrchestrationOptions_Brs_X01_InputExample_V1.OptionValue)}",
                expectedOptionValue
            },
        });
        Services.AddCustomOptions(assemblyToScan: ExampleOrchestrationsAssembly);

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();

        // Assert
        var option = serviceProvider.GetRequiredService<IOptions<OrchestrationOptions_Brs_X01_InputExample_V1>>();
        option.Should().NotBeNull();

        option!.Value.OptionValue.Should().Be(expectedOptionValue, "because the option value should depend on our configuration");
    }

    [Fact]
    public void AddCustomHandlersForServiceBusTriggers_WhenScanningExampleOrchestrations_ExpectedHandlersAreRegistered()
    {
        // Arrange
        Services.AddSingleton(Mock.Of<IStartOrchestrationInstanceMessageCommands>());
        Services.AddLogging();

        // Act
        Services.AddCustomHandlersForServiceBusTriggers(assemblyToScan: ExampleOrchestrationsAssembly);

        // Assert
        using var assertionScope = new AssertionScope();
        var serviceProvider = Services.BuildServiceProvider();
        var orchestrationInstanceFromMessageHandler = serviceProvider.GetRequiredService<IStartOrchestrationInstanceFromMessageHandler>();
        var stuff = orchestrationInstanceFromMessageHandler.Get().ToList();
        orchestrationInstanceFromMessageHandler.Should().NotBeNull();

        var startUpdateMeteringPointConnectionStateV1 = serviceProvider.GetRequiredService<Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.StartUpdateMeteringPointConnectionStateV1>();
        startUpdateMeteringPointConnectionStateV1.Should().NotBeNull();

        var startActorRequestProcessExampleHandlerV1 = serviceProvider.GetRequiredService<Example.Orchestrations.Processes.BRS_X02.ActorRequestProcessExample.V1.StartActorRequestProcessExampleHandlerV1>();
        startActorRequestProcessExampleHandlerV1.Should().NotBeNull();

        var notifyOrchestrationInstanceExampleHandlerV1 = serviceProvider.GetRequiredService<Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.StartNotifyOrchestrationInstanceExampleHandlerV1>();
        notifyOrchestrationInstanceExampleHandlerV1.Should().NotBeNull();
    }
}
