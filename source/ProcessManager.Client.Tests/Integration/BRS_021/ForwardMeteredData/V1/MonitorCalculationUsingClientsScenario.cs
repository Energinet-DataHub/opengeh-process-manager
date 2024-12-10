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

using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Client.Tests.Extensions;
using Energinet.DataHub.ProcessManager.Client.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using FluentAssertions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Integration.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ProcessManagerClientCollection))]
public class MonitorCalculationUsingClientsScenario : IAsyncLifetime
{
    private readonly ProcessManagerClientFixture _fixture;

    public MonitorCalculationUsingClientsScenario(
        ProcessManagerClientFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = _fixture.ProcessManagerTopic.Name,
        });
        services.AddAzureClients(
            b =>
            {
                b.AddServiceBusClientWithNamespace(_fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace);
            });
        services.AddProcessManagerMessageClient();
        ServiceProvider = services.BuildServiceProvider();
    }

    private ServiceProvider ServiceProvider { get; }

    public Task InitializeAsync()
    {
        _fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        _fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ForwardMeteredData_WhenStartedUsingClient_CanMonitorLifecycle()
    {
        // Arrange
        var input = CreateMeteredDataForMeasurementPointMessageInputV1();

        var startCommand = new StartForwardMeteredDataCommandV1(
            new ActorIdentityDto(input.AuthenticatedActorId),
            input,
            "test-message-id");

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();

        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-30);
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startCommand, CancellationToken.None);

        // Assert
        var orchestration = await _fixture.DurableClient.WaitForOrchestationStartedAsync(
            orchestrationCreatedAfter,
            name: "Orchestration_Brs_021_ForwardMeteredData_V1");
        var inputToken = JToken.FromObject(input);
        orchestration.Input.ToString().Should().BeEquivalentTo(inputToken.ToString(Newtonsoft.Json.Formatting.None));

        var completedOrchestration = await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestration.InstanceId);
        completedOrchestration.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);
    }

    private static MeteredDataForMeasurementPointMessageInputV1 CreateMeteredDataForMeasurementPointMessageInputV1()
    {
        var input = new MeteredDataForMeasurementPointMessageInputV1(
            Guid.NewGuid(),
            "EGU9B8E2630F9CB4089BDE22B597DFA4EA5",
            "571313101700011887",
            "D20",
            "8716867000047",
            "K3",
            "2024-12-03T08:00:00Z",
            "PT1H",
            "2024-12-01T23:00:00Z",
            "2024-12-02T23:00:00Z",
            "5790002606892",
            null,
            new List<EnergyObservation>()
            {
                new EnergyObservation("1", "112.000", "E01"),
                new EnergyObservation("2", "112.000", "E01"),
                new EnergyObservation("3", "112.000", "E01"),
                new EnergyObservation("4", "112.000", "E01"),
                new EnergyObservation("5", "112.000", "E01"),
                new EnergyObservation("6", "112.000", "E01"),
                new EnergyObservation("7", "112.000", "E01"),
                new EnergyObservation("8", "112.000", "E01"),
                new EnergyObservation("9", "112.000", "E01"),
                new EnergyObservation("10", "112.000", "E01"),
                new EnergyObservation("12", "112.000", "E01"),
                new EnergyObservation("12", "112.000", "E01"),
                new EnergyObservation("13", "112.000", "E01"),
                new EnergyObservation("14", "112.000", "E01"),
                new EnergyObservation("15", "112.000", "E01"),
                new EnergyObservation("16", "112.000", "E01"),
                new EnergyObservation("18", "112.000", "E01"),
                new EnergyObservation("19", "112.000", "E01"),
                new EnergyObservation("20", "112.000", "E01"),
                new EnergyObservation("21", "112.000", "E01"),
                new EnergyObservation("22", "112.000", "E01"),
                new EnergyObservation("23", "112.000", "E01"),
                new EnergyObservation("24", "112.000", "E01"),
            });
        return input;
    }
}
