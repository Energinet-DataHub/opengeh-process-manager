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
using Energinet.DataHub.ProcessManager.Components.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.EnqueueActorMessagesStep;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.Shared.CalculatedMeasurements.V1.Activities.EnqueueActorMessagesStep;

public class EnqueueActorMessageActivityTests
{
    [Fact]
    public async Task Run_ThrowsException_WhenQueryIsUnsuccessful()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1>>();
        var meteringPointMasterDataProviderMock = new Mock<IMeteringPointMasterDataProvider>();
        var meteringPointReceiversProviderMock = new Mock<IMeteringPointReceiversProvider>();
        var enqueueActorMessagesClientMock = new Mock<IEnqueueActorMessagesClient>();
        var databricksQueryOptionsMock = new Mock<IOptionsSnapshot<DatabricksQueryOptions>>();
        var databricksSqlWarehouseQueryExecutorMock = new Mock<DatabricksSqlWarehouseQueryExecutor>();
        var mockAsyncEnumerable = new Mock<IAsyncEnumerable<dynamic>>();
        var mockAsyncEnumerator = new Mock<IAsyncEnumerator<dynamic>>();

        mockAsyncEnumerator
            .SetupSequence(e => e.MoveNextAsync())
            .ReturnsAsync(true) // First call returns true
            .ReturnsAsync(false); // Subsequent call returns false

        mockAsyncEnumerator
            .Setup(e => e.Current)
            .Returns(new { t1 = 1, t2 = 2 });

        mockAsyncEnumerator
            .Setup(e => e.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        mockAsyncEnumerable
            .Setup(e => e.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(mockAsyncEnumerator.Object);

        databricksSqlWarehouseQueryExecutorMock
            .Setup(q => q.ExecuteStatementAsync(It.IsAny<DatabricksStatement>(), Format.JsonArray, It.IsAny<CancellationToken>()))
            .Returns(Test());

        var options = new DatabricksQueryOptions { DatabaseName = "dummy_database_name", CatalogName = "dummy_catalog_name" };
        databricksQueryOptionsMock.Setup(x => x.Get(QueryOptionsSectionNames.CalculatedMeasurementsQuery)).Returns(options);

        var activity = new EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1(
            loggerMock.Object,
            meteringPointMasterDataProviderMock.Object,
            meteringPointReceiversProviderMock.Object,
            enqueueActorMessagesClientMock.Object,
            databricksQueryOptionsMock.Object,
            databricksSqlWarehouseQueryExecutorMock.Object);

        var input = new EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1.ActivityInput(
            new OrchestrationInstanceId(Guid.NewGuid()));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => activity.Run(input));
    }

    private async IAsyncEnumerable<dynamic> Test()
    {
        yield return await Task.FromResult(new { t1 = 1, t2 = 2 });
    }
}
