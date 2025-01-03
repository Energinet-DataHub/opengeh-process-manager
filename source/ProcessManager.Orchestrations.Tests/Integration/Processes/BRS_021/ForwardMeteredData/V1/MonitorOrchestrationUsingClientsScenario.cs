﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using FluentAssertions;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_021.ForwardMeteredData.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// forward metered data orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class MonitorOrchestrationUsingClientsScenario : IAsyncLifetime
{
    private readonly OrchestrationsAppFixture _fixture;

    public MonitorOrchestrationUsingClientsScenario(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = _fixture.ProcessManagerTopicName,
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

    public async Task DisposeAsync()
    {
        _fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        _fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
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

        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-5);
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
                new("1", "112.000", "E01"),
                new("2", "112.000", "E01"),
                new("3", "112.000", "E01"),
                new("4", "112.000", "E01"),
                new("5", "112.000", "E01"),
                new("6", "112.000", "E01"),
                new("7", "112.000", "E01"),
                new("8", "112.000", "E01"),
                new("9", "112.000", "E01"),
                new("10", "112.000", "E01"),
                new("12", "112.000", "E01"),
                new("12", "112.000", "E01"),
                new("13", "112.000", "E01"),
                new("14", "112.000", "E01"),
                new("15", "112.000", "E01"),
                new("16", "112.000", "E01"),
                new("18", "112.000", "E01"),
                new("19", "112.000", "E01"),
                new("20", "112.000", "E01"),
                new("21", "112.000", "E01"),
                new("22", "112.000", "E01"),
                new("23", "112.000", "E01"),
                new("24", "112.000", "E01"),
            });
        return input;
    }
}
