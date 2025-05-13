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

using System.Globalization;
using Energinet.DataHub.Core.Databricks.SqlStatementExecution;
using Energinet.DataHub.Core.Databricks.SqlStatementExecution.Formats;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model;
using Energinet.DataHub.ProcessManager.Components.Time;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.Shared.MissingMeasurementsLog.V1.EnqueueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NodaTime;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_045.Shared.MissingMeaurementsLog.V1.EnqueueActorMessagesStep;

public class EnqueueActorMessageActivityTests
{
    private readonly DateTimeZone _timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull("Europe/Copenhagen")!;

    [Fact]
    public async Task Given_ReceivesTransactionsFromDatabricksQuery_When_ActivityIsRun_Then_AllTransactionsAreEnqueued()
    {
        // Given
        // => Use more than the max concurrency, to make sure the activity still succeeds even if the number of transactions
        // is higher than the max concurrency.
        const int transactionsCount = (int)(EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1.MaxConcurrency * 2.5);

        var enqueueActorMessageMock = new Mock<IEnqueueActorMessagesHttpClient>();

        var databricksSqlMock = new Mock<DatabricksSqlWarehouseQueryExecutor>();
        databricksSqlMock
            .Setup(sql => sql.ExecuteStatementAsync(
                It.IsAny<DatabricksStatement>(),
                It.IsAny<Format>(),
                It.IsAny<CancellationToken>()))
            .Returns(GenerateDatabricksSqlAsyncEnumerable(transactionsCount));

        var sut = CreateSut(
            enqueueActorMessageMock,
            databricksSqlMock);

        // When activity is run
        var enqueuedTransactionCount = await sut.Run(
            new EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1.ActivityInput(
                new OrchestrationInstanceId(Guid.NewGuid())));

        // Then all transactions are enqueued
        Assert.Equal(transactionsCount, enqueuedTransactionCount);

        enqueueActorMessageMock.Verify(
            expression: m => m.EnqueueAsync(It.IsAny<EnqueueMissingMeasurementsLogHttpV1>()),
            times: Times.Exactly(transactionsCount));
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

        var transactionId1 = Guid.NewGuid();
        var row1 = CreateRowDictionary(
            new DatabricksSqlStatementApiCalculatedMeasurementsExtensions.CalculatedMeasurementsRowData(
                OrchestrationInstanceId: Guid.NewGuid(),
                TransactionId: transactionId1,
                TransactionCreationDatetime: Instant.FromUtc(2025, 05, 02, 13, 00),
                MeteringPointId: "1234567890123456",
                MeteringPointType: "electrical_heating",
                Resolution: "PT15M",
                ObservationTime: Instant.FromUtc(2025, 05, 02, 13, 00),
                Quantity: 1337.42m));

        var transactionId2 = Guid.NewGuid();
        var row2 = CreateRowDictionary(
            new DatabricksSqlStatementApiCalculatedMeasurementsExtensions.CalculatedMeasurementsRowData(
                OrchestrationInstanceId: Guid.NewGuid(),
                TransactionId: transactionId2,
                TransactionCreationDatetime: Instant.FromUtc(2025, 05, 02, 13, 00),
                MeteringPointId: "1234567890123456",
                MeteringPointType: "electrical_heating",
                Resolution: "PT15M",
                ObservationTime: Instant.FromUtc(2025, 05, 02, 13, 00),
                Quantity: 1337.42m));

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

        // Then the activity throws an exception containing the failed transaction id (and not the succeeded)
        var thrownException = await Assert.ThrowsAsync<Exception>(act);

        // Assert that the exception message contains one of the transaction id's, but not both.
        // We need it like this because the transaction are processed in parallel, so we don't know which one will fail.
        Assert.Multiple(
            () => Assert.True(
                condition: thrownException.Message.Contains(transactionId1.ToString())
                           || thrownException.Message.Contains(transactionId2.ToString()),
                userMessage: "The exception message should contain one of the two transaction id's, because one of them should fail."),
            () => Assert.False(
                condition: thrownException.Message.Contains(transactionId1.ToString())
                           && thrownException.Message.Contains(transactionId2.ToString()),
                userMessage: "The exception message should not contain both transaction id's, because one of them should succeed."));

        // And then enqueue is called for each transaction, to make sure the 2nd transaction is still enqueued
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
        var clock = MockClock();
        var timeHelper = MockTimeHelper();


        var sut = new EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1(
            new Mock<ILogger<EnqueueActorMessageActivity_Brs_045_Shared_MissingMeasurementsLog_V1>>().Object,
            masterDataProviderMock.Object,
            new MeteringPointReceiversProvider(_timeZone),
            enqueueActorMessageMock.Object,
            queryOptionsMock.Object,
            databricksSqlMock.Object,
            timeHelper.Object,
            clock.Object);

        return sut;
    }

    private object MockTimeHelper()
    {
        return new Mock<TimeHelper>();
    }

    private object MockClock()
    {
        return new Mock<IClock>();
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
                    new DatabricksSqlStatementApiCalculatedMeasurementsExtensions.CalculatedMeasurementsRowData(
                        OrchestrationInstanceId: Guid.NewGuid(),
                        TransactionId: Guid.NewGuid(),
                        TransactionCreationDatetime: Instant.FromUtc(2025, 05, 02, 13, 00),
                        MeteringPointId: "1234567890123456",
                        MeteringPointType: "electrical_heating",
                        Resolution: "PT15M",
                        // Skipping 30 minutes, when the resolution is 15 minutes, creates a gap between each observation,
                        // and thus each observation has a new transaction id.
                        ObservationTime: start.Plus(Duration.FromMinutes(30 * i)),
                        Quantity: 1337.42m))
            .ToList();

        var rowsAsDictionaries = rows
            .Select(CreateRowDictionary)
            .ToAsyncEnumerable();

        return rowsAsDictionaries;
    }

    private Dictionary<string, object> CreateRowDictionary(
        DatabricksSqlStatementApiCalculatedMeasurementsExtensions.CalculatedMeasurementsRowData data)
    {
        var schemaDescription = new MissingMeasurementsLogSchemaDescription(Mock.Of<DatabricksQueryOptions>());

        return schemaDescription.SchemaDefinition.Keys.ToDictionary<string, string, object>(
            keySelector: columnName => columnName,
            elementSelector: columnName => columnName switch
            {
                CalculatedMeasurementsColumnNames.OrchestrationType => "unused",
                CalculatedMeasurementsColumnNames.OrchestrationInstanceId => data.OrchestrationInstanceId.ToString(),
                CalculatedMeasurementsColumnNames.TransactionId => data.TransactionId.ToString(),
                CalculatedMeasurementsColumnNames.TransactionCreationDatetime => data.TransactionCreationDatetime.ToString(),
                CalculatedMeasurementsColumnNames.MeteringPointId => data.MeteringPointId,
                CalculatedMeasurementsColumnNames.MeteringPointType => data.MeteringPointType,
                CalculatedMeasurementsColumnNames.ObservationTime => data.ObservationTime.ToString(),
                CalculatedMeasurementsColumnNames.Quantity => data.Quantity.ToString(NumberFormatInfo.InvariantInfo),
                CalculatedMeasurementsColumnNames.QuantityUnit => data.QuantityUnit,
                CalculatedMeasurementsColumnNames.QuantityQuality => data.QuantityQuality,
                CalculatedMeasurementsColumnNames.Resolution => data.Resolution,
                _ => throw new ArgumentOutOfRangeException(nameof(columnName), $"Unknown column name: {columnName}"),
            });
    }
}
