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

using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.Core.Databricks.SqlStatementExecution.Formats;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.Shared;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.MissingMeasurementsLog.V1.EnqueueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_045.Shared.MissingMeasurementsLog.V1.EnqueueActorMessagesStep;

public class EnqueueActorMessageActivityTests
{
    private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;
    private readonly HashSet<string> _generatedIds = new();

    [Fact]
    public async Task Given_ReceivesTransactionsFromDatabricksQuery_When_ActivityIsRun_Then_AllTransactionAreEnqueued()
    {
        // Given
        // => Use more than the max concurrency, to make sure the activity still succeeds even if the number of transactions
        // is higher than the max concurrency.
        const int transactionCount = (int)(EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1.MaxConcurrency * 2.5);
        var enqueueActorMessageMock = new Mock<IEnqueueActorMessagesHttpClient>();
        var databricksSqlMock = new Mock<DatabricksSqlWarehouseQueryExecutor>();
        databricksSqlMock
            .Setup(sql => sql.ExecuteStatementAsync(
                It.IsAny<DatabricksStatement>(),
                It.IsAny<Format>(),
                It.IsAny<CancellationToken>()))
            .Returns(GenerateDatabricksSqlAsyncEnumerable(transactionCount));

        var sut = CreateSut(
            enqueueActorMessageMock,
            databricksSqlMock);

        // When activity is run
        var enqueuedTransactionsCount = await sut.Run(
            new EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.NewGuid())));

        // Then all transactions are enqueued
        Assert.Equal(transactionCount, enqueuedTransactionsCount);

        enqueueActorMessageMock.Verify(
            expression: m => m.EnqueueAsync(It.IsAny<EnqueueMissingMeasurementsLogHttpV1>()),
            times: Times.Exactly(transactionCount));
    }

    [Fact]
    public async Task Given_FirstCallToEnqueueFails_When_ActivityIsRun_Then_ExceptionIsThrown_AndThen_FirstTransactionFailsButOthersAreStillEnqueued()
    {
        // Given
        var enqueueActorMessageMock = new Mock<IEnqueueActorMessagesHttpClient>();
        enqueueActorMessageMock.SetupSequence(
                s => s.EnqueueAsync(It.IsAny<EnqueueMissingMeasurementsLogHttpV1>()))
            .Throws(new Exception("Unhandled exception")) // 1st call fails
            .Returns(Task.CompletedTask); // 2nd call succeeds

        const string meteringPointId = "1000000000000001";
        var row1 = CreateRowDictionary(
            new DatabricksSqlStatementApiMissingMeasurementsLogExtensions.MissingMeasurementsLogRowData(
                OrchestrationInstanceId: Guid.NewGuid(),
                MeteringPointId: meteringPointId,
                Date: Instant.FromUtc(2025, 5, 2, 13, 0)));

        const string meteringPointId2 = "1000000000000002";
        var row2 = CreateRowDictionary(
            new DatabricksSqlStatementApiMissingMeasurementsLogExtensions.MissingMeasurementsLogRowData(
                OrchestrationInstanceId: Guid.NewGuid(),
                MeteringPointId: meteringPointId2,
                Date: Instant.FromUtc(2025, 5, 2, 13, 15)));

        var mockTransactions = new List<Dictionary<string, object>>
        {
            row1,
            row2,
        };

        var databricksSqlMock = new Mock<DatabricksSqlWarehouseQueryExecutor>();
        databricksSqlMock
            .Setup(sql => sql.ExecuteStatementAsync(
                It.IsAny<DatabricksStatement>(),
                It.IsAny<Format>(),
                It.IsAny<CancellationToken>()))
            .Returns(mockTransactions.ToAsyncEnumerable);

        var sut = CreateSut(
            enqueueActorMessageMock,
            databricksSqlMock);

        // When activity is run
        var act = () => sut.Run(
            new EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.NewGuid())));

        // Then the activity throws an exception containing the failed missing measurements log (and not the succeeded)
        var thrownException = await Assert.ThrowsAsync<Exception>(act);

        // Assert that the exception message contains one of the missing measurements logs, but not both.
        // We need it like this because the missing measurements logs are processed in parallel, so we don't know which one will fail.
        Assert.Multiple(
            () => Assert.True(
                condition: thrownException.Message.Contains(meteringPointId) || thrownException.Message.Contains(meteringPointId2),
                userMessage: "The exception message should contain one of the two missing measurements logs's, because one of them should fail."),
            () => Assert.False(
                condition: thrownException.Message.Contains(meteringPointId)
                           && thrownException.Message.Contains(meteringPointId2),
                userMessage: "The exception message should not contain both missing measurements logs's, because one of them should succeed."));

        // And then enqueue is called for each missing measurements logs
        enqueueActorMessageMock.Verify(
            expression: m => m.EnqueueAsync(It.IsAny<EnqueueMissingMeasurementsLogHttpV1>()),
            times: Times.Exactly(mockTransactions.Count));
    }

    private EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1 CreateSut(
        Mock<IEnqueueActorMessagesHttpClient> enqueueActorMessageMock,
        Mock<DatabricksSqlWarehouseQueryExecutor> databricksSqlMock)
    {
        var masterDataProviderMock = MockMasterDataProvider();
        var queryOptionsMock = MockQueryOptions();
        var sut = new EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1(
            new Mock<ILogger<EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1>>().Object,
            masterDataProviderMock.Object,
            new MeteringPointReceiversProvider(_timeZone),
            enqueueActorMessageMock.Object,
            queryOptionsMock.Object,
            databricksSqlMock.Object);

        return sut;
    }

    private Mock<IOptionsSnapshot<DatabricksQueryOptions>> MockQueryOptions()
    {
        var queryOptionsMock = new Mock<IOptionsSnapshot<DatabricksQueryOptions>>();
        queryOptionsMock
            .Setup(o => o.Get(It.IsAny<string>()))
            .Returns(new DatabricksQueryOptions { CatalogName = "TestCatalog", DatabaseName = "TestDatabase" });
        return queryOptionsMock;
    }

    private Mock<IMeteringPointMasterDataProvider> MockMasterDataProvider()
    {
        var masterDataProviderMock = new Mock<IMeteringPointMasterDataProvider>();
        masterDataProviderMock.Setup(
                mdp => mdp.GetMasterData(
                    It.IsAny<string>(),
                    It.IsAny<Instant>(),
                    It.IsAny<Instant>()))
            .Returns(
                Task.FromResult<IReadOnlyCollection<MeteringPointMasterData>>(
                [
                    new(
                        MeteringPointId: new MeteringPointId("1114567890123456"),
                        ValidFrom: DateTimeOffset.MinValue,
                        ValidTo: DateTimeOffset.MaxValue,
                        CurrentGridAreaCode: new GridAreaCode("404"),
                        CurrentGridAccessProvider: ActorNumber.Create("1111111111111"),
                        CurrentNeighborGridAreaOwners: [],
                        ConnectionState: ConnectionState.Connected,
                        MeteringPointType: MeteringPointType.ElectricalHeating,
                        MeteringPointSubType: MeteringPointSubType.Calculated,
                        Resolution: Resolution.QuarterHourly,
                        MeasurementUnit: MeasurementUnit.KilowattHour,
                        ProductId: "???",
                        ParentMeteringPointId: new MeteringPointId("2224567890123456"),
                        EnergySupplier: ActorNumber.Create("2222222222222")),
                ]));
        return masterDataProviderMock;
    }

    private IAsyncEnumerable<IDictionary<string, object>> GenerateDatabricksSqlAsyncEnumerable(int transactionsCount)
    {
        var start = Instant.FromUtc(2025, 05, 02, 12, 00);
        var rows = Enumerable.Range(0, transactionsCount)
            .Select(
                i =>
                    new DatabricksSqlStatementApiMissingMeasurementsLogExtensions.MissingMeasurementsLogRowData(
                        OrchestrationInstanceId: Guid.NewGuid(),
                        MeteringPointId: GenerateUniqueMeteringPointId(),
                        Date: start.Plus(Duration.FromMinutes(15 * i))))
            .ToList();

        var rowsAsDictionaries = rows
            .Select(CreateRowDictionary)
            .ToAsyncEnumerable();

        return rowsAsDictionaries;
    }

    private string GenerateUniqueMeteringPointId()
    {
        var random = new Random();
        string id;

        do
        {
            id = $"1000{random.Next(100000000, 1000000000):D9}";
        }
        while (!_generatedIds.Add(id)); // Ensure uniqueness by adding to the set

        return id;
    }

    private Dictionary<string, object> CreateRowDictionary(
        DatabricksSqlStatementApiMissingMeasurementsLogExtensions.MissingMeasurementsLogRowData data)
    {
        var schemaDescription = new MissingMeasurementsLogSchemaDescription(Mock.Of<DatabricksQueryOptions>());

        return schemaDescription.SchemaDefinition.Keys.ToDictionary<string, string, object>(
            keySelector: columnName => columnName,
            elementSelector: columnName => columnName switch
            {
                MissingMeasurementsLogColumnNames.OrchestrationInstanceId => data.OrchestrationInstanceId.ToString(),
                MissingMeasurementsLogColumnNames.MeteringPointId => data.MeteringPointId,
                MissingMeasurementsLogColumnNames.Date => data.Date.ToString(),
                _ => throw new ArgumentOutOfRangeException(nameof(columnName), $"Unknown column name: {columnName}"),
            });
    }
}
