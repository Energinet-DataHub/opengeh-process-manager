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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_026.V1;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to start a
/// request calculated energy time series orchestration and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class RequestCalculatedEnergyTimeSeriesTests : IAsyncLifetime
{
    private readonly OrchestrationsAppFixture _fixture;

    public RequestCalculatedEnergyTimeSeriesTests(
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
            builder => builder.AddServiceBusClientWithNamespace(_fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
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
    public async Task RequestCalculatedEnergyTimeSeries_WhenStarted_OrchestrationCompletesWithSuccess()
    {
        // Arrange
        var businessReason = "BalanceFixing";
        var energySupplierNumber = "23143245321";
        var startRequestCommand = new RequestCalculatedEnergyTimeSeriesCommandV1(
            new ActorIdentityDto(Guid.NewGuid()),
            new RequestCalculatedEnergyTimeSeriesInputV1(
                RequestedForActorNumber: energySupplierNumber,
                RequestedForActorRole: "EnergySupplier",
                BusinessReason: businessReason,
                PeriodStart: "2024-04-07 23:00:00",
                PeriodEnd: "2024-04-08 23:00:00",
                EnergySupplierNumber: energySupplierNumber,
                BalanceResponsibleNumber: null,
                GridAreas: ["804"],
                MeteringPointType: null,
                SettlementMethod: null,
                SettlementVersion: null),
            idempotencyKey: Guid.NewGuid().ToString());

        var processManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();

        // Act
        var orchestrationCreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await processManagerMessageClient.StartNewOrchestrationInstanceAsync(startRequestCommand, CancellationToken.None);

        // Assert
        // => Orchestration is started
        var orchestration = await _fixture.DurableClient.WaitForOrchestationStartedAsync(
            createdTimeFrom: orchestrationCreatedAfter,
            name: nameof(Orchestration_Brs_026_V1));
        orchestration.Input.ToString().Should().Contain(businessReason);

        // => Orchestration is waiting for notify event
        await _fixture.DurableClient.WaitForCustomStatusAsync<string>(
            orchestration.InstanceId,
            (customStatus) => customStatus == Orchestration_Brs_026_V1.CustomStatus.WaitingForEnqueueActorMessages);
        // => Send notify event
        await processManagerMessageClient.NotifyOrchestrationInstanceAsync(
            new NotifyOrchestrationInstanceEvent(
                orchestration.InstanceId,
                RequestCalculatedEnergyTimeSeriesNotifyEventsV1.EnqueueActorMessagesCompleted),
            CancellationToken.None);

        // => Orchestration is completed (with success)
        var completedOrchestration = await _fixture.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestration.InstanceId,
            waitTimeLimit: TimeSpan.FromMinutes(1));
        completedOrchestration.RuntimeStatus.Should().Be(OrchestrationRuntimeStatus.Completed);
        completedOrchestration.Output.ToString().Should().Contain("Success");
    }
}
