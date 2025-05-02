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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Extensions.Options;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.Shared.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.Options;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.Databricks.SqlStatements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.CalculatedMeasurements.V1.EnqueueActorMessagesStep;

public class EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1(
    ILogger<EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1> logger,
    MeteringPointMasterDataProvider meteringPointMasterDataProvider,
    MeteringPointReceiversProvider meteringPointReceiversProvider,
    IEnqueueActorMessagesHttpClient enqueueActorMessagesHttpClient,
    IOptionsSnapshot<DatabricksQueryOptions> databricksQueryOptions,
    DatabricksSqlWarehouseQueryExecutor databricksSqlWarehouseQueryExecutor)
{
    private readonly ILogger<EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1> _logger = logger;
    private readonly MeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;
    private readonly IEnqueueActorMessagesHttpClient _enqueueActorMessagesHttpClient = enqueueActorMessagesHttpClient;
    private readonly DatabricksQueryOptions _databricksQueryOptions = databricksQueryOptions.Get(QueryOptionsSectionNames.CalculatedMeasurementsQuery);
    private readonly DatabricksSqlWarehouseQueryExecutor _databricksSqlWarehouseQueryExecutor = databricksSqlWarehouseQueryExecutor;

    [Function(nameof(EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1))]
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
        var enqueueTasks = new List<Task>();
        await foreach (var queryResult in query.GetAsync(_databricksSqlWarehouseQueryExecutor).ConfigureAwait(false))
        {
            if (!queryResult.IsSuccess || queryResult.Result is null) // TODO: Actually handle errors
                throw new Exception("Failed to get calculated measure data.");

            // The query result measure data is already grouped by transaction id, so
            // we need to find receivers for it based on the master data for the metering point.
            var enqueueTask = EnqueueMessagesForMeasureDataAsync(
                    orchestrationInstanceId: input.OrchestrationInstanceId,
                    calculatedMeasureData: queryResult.Result);

            enqueueTasks.Add(enqueueTask);

            // Max 100 running EnqueueActorMessage tasks at the same time, to avoid DDOSing the enqueue actor messages HTTP service.
            if (enqueueTasks.Count(t => !t.IsCompleted) > 100)
                await Task.WhenAll(enqueueTasks).ConfigureAwait(false);
        }

        await Task.WhenAll(enqueueTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Enqueue calculated measure data for a metering point.
    /// <remarks>
    /// The measure data MUST be ordered by timestamp, and MUST NOT contain any gaps.
    /// </remarks>
    /// </summary>
    private async Task EnqueueMessagesForMeasureDataAsync(
        OrchestrationInstanceId orchestrationInstanceId,
        CalculatedMeasurement calculatedMeasureData)
    {
        var period = GetMeasurementsPeriod(calculatedMeasureData);

        var receiversWithMeasurements = await FindReceiversForMeasureDataAsync(
                calculatedMeasureData,
                period.Start,
                period.End)
            .ConfigureAwait(false);

        await EnqueueActorMessagesAsync(
                orchestrationInstanceId,
                calculatedMeasureData,
                receiversWithMeasurements,
                period)
            .ConfigureAwait(false);
    }

    private Interval GetMeasurementsPeriod(CalculatedMeasurement calculatedMeasureData)
    {
        var from = calculatedMeasureData.MeasureData.First().ObservationTime;

        var resolutionAsDuration = calculatedMeasureData.Resolution switch
        {
            var r when r == Resolution.QuarterHourly => Duration.FromMinutes(15),
            var r when r == Resolution.Hourly => Duration.FromHours(1),
            // TODO: What are the possible resolutions for the calculated measurements?
            _ => throw new ArgumentOutOfRangeException(nameof(calculatedMeasureData.Resolution), calculatedMeasureData.Resolution, "Invalid resolution"),
        };
        var to = calculatedMeasureData.MeasureData.Last().ObservationTime.Plus(resolutionAsDuration);

        return new Interval(from, to);
    }

    private async Task<List<ReceiversWithMeasureData>> FindReceiversForMeasureDataAsync(CalculatedMeasurement calculatedMeasureData, Instant from, Instant to)
    {
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

        return receiversForMeteringPoint;
    }

    private async Task EnqueueActorMessagesAsync(
        OrchestrationInstanceId orchestrationInstanceId,
        CalculatedMeasurement calculatedMeasureData,
        List<ReceiversWithMeasureData> receiversWithMeasurements,
        Interval measurementsPeriod)
    {
        var enqueueData = new EnqueueCalculatedMeasurementsHttpV1(
            OrchestrationInstanceId: orchestrationInstanceId.Value,
            TransactionId: calculatedMeasureData.TransactionId,
            MeteringPointId: calculatedMeasureData.MeteringPointId,
            MeteringPointType: calculatedMeasureData.MeteringPointType,
            Resolution: calculatedMeasureData.Resolution,
            MeasureUnit: MeasurementUnit.KilowattHour,
            Data: receiversWithMeasurements.Select(
                    r => new EnqueueCalculatedMeasurementsHttpV1.ReceiversWithMeasurements(
                        Receivers: r.Receivers
                            .Select(
                                actor => new EnqueueCalculatedMeasurementsHttpV1.Actor(
                                    ActorNumber.Create(actor.Number.Value),
                                    ActorRole.FromName(actor.Role.Name)))
                            .ToList(),
                        RegistrationDateTime: calculatedMeasureData.TransactionCreationDatetime.ToDateTimeOffset(), // TODO: Correct?
                        StartDateTime: measurementsPeriod.Start.ToDateTimeOffset(),
                        EndDateTime: measurementsPeriod.End.ToDateTimeOffset(),
                        Measurements: r.MeasureDataList
                            .Select(
                                (md, i) => new EnqueueCalculatedMeasurementsHttpV1.Measurement(
                                    Position: md.Position,
                                    // TODO: Are these null assumptions correct?
                                    EnergyQuantity: md.EnergyQuantity ?? throw new InvalidOperationException("Energy quantity should not be null in calculated measurement calculations."),
                                    QuantityQuality: md.QuantityQuality ?? throw new InvalidOperationException("Quality should not be null in calculated measurement calculations.")))
                            .ToList(),
                        GridAreaCode: r.GridArea))
                .ToList());

        await _enqueueActorMessagesHttpClient.EnqueueAsync(enqueueData).ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
