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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Client;
using Energinet.DataHub.ProcessManager.Client.Extensions.DependencyInjection;
using Energinet.DataHub.ProcessManager.Client.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.CustomQueries;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_026.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_026_028.CustomQueries;

/// <summary>
/// Test collection that verifies the Process Manager clients can be used to
/// perform a custom search for BRS 026 + 028 orchestration instances.
/// </summary>
[Collection(nameof(OrchestrationsAppCollection))]
public class SearchTrigger_Brs_026_028Tests : IAsyncLifetime
{
    public SearchTrigger_Brs_026_028Tests(
        OrchestrationsAppFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);

        var services = new ServiceCollection();
        services.AddInMemoryConfiguration(new Dictionary<string, string?>
        {
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.GeneralApiBaseAddress)}"]
                = Fixture.ProcessManagerAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerHttpClientsOptions.SectionName}:{nameof(ProcessManagerHttpClientsOptions.OrchestrationsApiBaseAddress)}"]
                = Fixture.OrchestrationsAppManager.AppHostManager.HttpClient.BaseAddress!.ToString(),
            [$"{ProcessManagerServiceBusClientOptions.SectionName}:{nameof(ProcessManagerServiceBusClientOptions.TopicName)}"]
                = Fixture.ProcessManagerTopicName,
        });
        services.AddProcessManagerHttpClients();
        services.AddAzureClients(
            builder => builder.AddServiceBusClientWithNamespace(Fixture.IntegrationTestConfiguration.ServiceBusFullyQualifiedNamespace));
        services.AddProcessManagerMessageClient();
        ServiceProvider = services.BuildServiceProvider();

        ProcessManagerClient = ServiceProvider.GetRequiredService<IProcessManagerClient>();
        ProcessManagerMessageClient = ServiceProvider.GetRequiredService<IProcessManagerMessageClient>();
    }

    private OrchestrationsAppFixture Fixture { get; }

    private ServiceProvider ServiceProvider { get; }

    private IProcessManagerClient ProcessManagerClient { get; }

    private IProcessManagerMessageClient ProcessManagerMessageClient { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        await ServiceProvider.DisposeAsync();
    }

    /// <summary>
    /// This test proves that we can get type strong result objects return from the custom query.
    /// </summary>
    [Fact]
    public async Task Brs026And028OrchestrationInstancesInDatabase_WhenQueryActorRequests_ExpectedResultTypesAreRetrieved()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        var energySupplierActorIdentity = new ActorIdentityDto(
            ActorNumber.Create("23143245321"),
            ActorRole.EnergySupplier);

        // => Brs 026
        var brs026Input = new RequestCalculatedEnergyTimeSeriesInputV1(
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString(),
            RequestedForActorNumber: energySupplierActorIdentity.ActorNumber.Value,
            RequestedForActorRole: energySupplierActorIdentity.ActorRole.Name,
            RequestedByActorNumber: energySupplierActorIdentity.ActorNumber.Value,
            RequestedByActorRole: energySupplierActorIdentity.ActorRole.Name,
            BusinessReason: "BalanceFixing",
            PeriodStart: "2024-04-07 23:00:00",
            PeriodEnd: "2024-04-08 23:00:00",
            EnergySupplierNumber: energySupplierActorIdentity.ActorNumber.Value,
            BalanceResponsibleNumber: null,
            GridAreas: ["804"],
            MeteringPointType: null,
            SettlementMethod: null,
            SettlementVersion: null);
        var startRequestCalculatedEnergyTimeSeriesCommand = new RequestCalculatedEnergyTimeSeriesCommandV1(
            energySupplierActorIdentity,
            brs026Input,
            idempotencyKey: Guid.NewGuid().ToString());

        var orchestrationBrs026CreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCalculatedEnergyTimeSeriesCommand,
            cancellationToken: default);

        await Fixture.DurableClient.WaitForOrchestationStartedAsync(
            createdTimeFrom: orchestrationBrs026CreatedAfter,
            name: nameof(Orchestration_Brs_026_V1));

        // => Brs 028
        var brs028Input = new RequestCalculatedWholesaleServicesInputV1(
            ActorMessageId: Guid.NewGuid().ToString(),
            TransactionId: Guid.NewGuid().ToString(),
            RequestedForActorNumber: energySupplierActorIdentity.ActorNumber.Value,
            RequestedForActorRole: energySupplierActorIdentity.ActorRole.Name,
            RequestedByActorNumber: energySupplierActorIdentity.ActorNumber.Value,
            RequestedByActorRole: energySupplierActorIdentity.ActorRole.Name,
            BusinessReason: "WholesaleFixing",
            PeriodStart: "2024-04-01 23:00:00",
            PeriodEnd: "2024-04-30 23:00:00",
            Resolution: null,
            EnergySupplierNumber: energySupplierActorIdentity.ActorNumber.Value,
            ChargeOwnerNumber: null,
            GridAreas: ["804"],
            SettlementVersion: null,
            ChargeTypes: null);
        var startRequestCalculatedWholesaleServicesCommand = new RequestCalculatedWholesaleServicesCommandV1(
            energySupplierActorIdentity,
            brs028Input,
            idempotencyKey: Guid.NewGuid().ToString());

        var orchestrationBrs028CreatedAfter = DateTime.UtcNow.AddSeconds(-1);
        await ProcessManagerMessageClient.StartNewOrchestrationInstanceAsync(
            startRequestCalculatedWholesaleServicesCommand,
            cancellationToken: default);

        await Fixture.DurableClient.WaitForOrchestationStartedAsync(
            createdTimeFrom: orchestrationBrs028CreatedAfter,
            name: nameof(Orchestration_Brs_028_V1));

        // => Custom query
        var customQuery = new ActorRequestQuery(
            new UserIdentityDto(
                UserId: Guid.NewGuid(),
                ActorNumber: energySupplierActorIdentity.ActorNumber,
                ActorRole: energySupplierActorIdentity.ActorRole),
            activatedAtOrLater: now,
            activatedAtOrEarlier: now.AddMinutes(1),
            createdByActorNumber: energySupplierActorIdentity.ActorNumber,
            createdByActorRole: energySupplierActorIdentity.ActorRole);

        // Act
        var actual = await ProcessManagerClient
            .SearchOrchestrationInstancesByCustomQueryAsync(
                customQuery,
                CancellationToken.None);

        // Assert
        // TODO:
        // We could improve this test by using the "Idempotency Key" to compare from the "request",
        // but currently this is not supported when using the ProcessManager over ServiceBus
        using var assertionScope = new AssertionScope();
        actual.Should()
            .Contain(x =>
                x.GetType() == typeof(RequestCalculatedEnergyTimeSeriesResult))
            .And.Contain(x =>
                x.GetType() == typeof(RequestCalculatedWholesaleServicesResult));
    }
}
