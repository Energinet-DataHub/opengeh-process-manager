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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.EnqueueActorMessagesStep;

public class EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1(
    ILogger<EnqueueActorMessageActivity_Brs_021_CalculatedMeasurementsCalculation_V1> logger,
    MeteringPointMasterDataProvider meteringPointMasterDataProvider,
    MeteringPointReceiversProvider meteringPointReceiversProvider,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    IOptionsSnapshot<DatabricksQueryOptions> databricksQueryOptions,
    DatabricksSqlWarehouseQueryExecutor databricksSqlWarehouseQueryExecutor)
{
    private readonly ILogger<EnqueueActorMessageActivity_Brs_021_CalculatedMeasurementsCalculation_V1> _logger = logger;
    private readonly MeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly DatabricksQueryOptions _databricksQueryOptions = databricksQueryOptions.Get(QueryOptionsSectionNames.CalculatedMeasurementsQuery);
    private readonly DatabricksSqlWarehouseQueryExecutor _databricksSqlWarehouseQueryExecutor = databricksSqlWarehouseQueryExecutor;

    [Function(nameof(EnqueueActorMessageActivity_Brs_021_CalculatedMeasurementsCalculation_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var schemaDescription = new CalculatedMeasurementsSchemaDescription(_databricksQueryOptions);
        var query = new CalculatedMeasurementsQuery(
            _logger,
            schemaDescription,
            input.OrchestrationInstanceId.Value);

        // This queries all data sequentially, but that probably won't be as quick as we need.
        // TODO: How do we parallelize the query? What parameters can we use to split the query?
        await foreach (var queryResult in query.GetAsync(_databricksSqlWarehouseQueryExecutor).ConfigureAwait(false))
        {
            if (!queryResult.IsSuccess || queryResult.Result is null) // TODO: Actually handle errors
                throw new Exception("Failed to get calculated measure data.");

            // The query result measure data is already grouped by transaction id, so
            // we need to find receivers for it based on the master data for the metering point.
            await EnqueueMessagesForMeasureData(
                    orchestrationInstanceId: input.OrchestrationInstanceId,
                    calculatedMeasureData: queryResult.Result)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Enqueue calculated measure data for a metering point.
    /// <remarks>
    /// The measure data MUST be ordered by timestamp, and MUST NOT contain any gaps.
    /// </remarks>
    /// </summary>
    private async Task EnqueueMessagesForMeasureData(
        OrchestrationInstanceId orchestrationInstanceId,
        CalculatedMeasurement calculatedMeasureData)
    {
        var from = calculatedMeasureData.MeasureData.First().ObservationTime;
        var to = calculatedMeasureData.MeasureData.Last().ObservationTime;

        // We need to get master data & receivers for each metering point id
        var masterDataForMeteringPoint = await _meteringPointMasterDataProvider.GetMasterData(
                meteringPointId: calculatedMeasureData.MeteringPointId,
                startDateTime: from,
                endDateTime: to)
            .ConfigureAwait(false);

        var receiversForMeteringPoint = _meteringPointReceiversProvider
            .GetReceiversWithMeteredDataFromMasterDataList(
                new MeteringPointReceiversProvider.FindReceiversInput(
                    MeteringPointId: calculatedMeasureData.MeteringPointId,
                    StartDateTime: from,
                    EndDateTime: to,
                    Resolution: calculatedMeasureData.Resolution,
                    MasterData: masterDataForMeteringPoint,
                    MeasureData: calculatedMeasureData.MeasureData
                        .Select((md, i) => new ReceiversWithMeasureData.MeasureData(
                            Position: i + 1, // Position is 1-based, so the first position must be 1.
                            EnergyQuantity: md.Quantity,
                            QuantityQuality: Quality.Calculated))
                        .ToList()));

        // Enqueue to EDI
        // TODO: This needs to be handled differently, since this requires the orchestration to wait
        // for each enqueued messages event to be returned.
        // await _enqueueActorMessagesClient.EnqueueAsync(
        //         orchestration: Orchestration_Brs_021_ElectricalHeatingCalculation_V1.UniqueName,
        //         orchestrationInstanceId: orchestrationInstanceId.Value,
        //         orchestrationStartedBy: new ActorIdentityDto(ActorNumber.Create("1234567890123"), ActorRole.GridAccessProvider), // TODO: Get this from the orchestration instance
        //         // TODO: We need to create unique deterministic idempotency keys for each message sent to EDI instead,
        //         // so rerunning the activity generates the same idempotency keys as previous run.
        //         idempotencyKey: Guid.NewGuid(),
        //         data: new EnqueueActorMessagesForMeteringPointV1(
        //             ReceiversWithMeasureData: receiversForMeteringPoint.ToElectricalHeatingReceiversWithMeasureDataV1()))
        //     .ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
