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

using System.Collections.Concurrent;
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
    IMeteringPointMasterDataProvider meteringPointMasterDataProvider,
    MeteringPointReceiversProvider meteringPointReceiversProvider,
    IEnqueueActorMessagesHttpClient enqueueActorMessagesHttpClient,
    IOptionsSnapshot<DatabricksQueryOptions> databricksQueryOptions,
    DatabricksSqlWarehouseQueryExecutor databricksSqlWarehouseQueryExecutor)
{
    internal const int MaxConcurrency = 100; // Consider moving to an options class

    private readonly ILogger<EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1> _logger = logger;
    private readonly IMeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;
    private readonly IEnqueueActorMessagesHttpClient _enqueueActorMessagesHttpClient = enqueueActorMessagesHttpClient;
    private readonly DatabricksQueryOptions _databricksQueryOptions = databricksQueryOptions.Get(QueryOptionsSectionNames.CalculatedMeasurementsQuery);
    private readonly DatabricksSqlWarehouseQueryExecutor _databricksSqlWarehouseQueryExecutor = databricksSqlWarehouseQueryExecutor;

    [Function(nameof(EnqueueActorMessageActivity_Brs_021_Shared_CalculatedMeasurements_V1))]
    public async Task<int> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var schemaDescription = new CalculatedMeasurementsSchemaDescription(_databricksQueryOptions);
        var query = new CalculatedMeasurementsQuery(
            _logger,
            schemaDescription,
            input.OrchestrationInstanceId.Value);

        var enqueueTasks = new List<Task>();
        var failedTransactions = new ConcurrentBag<string>();
        var enqueuedTransactionsCount = 0;

        // Perform calls async, but only allow 100 to be running at the same time. Uses SemaphoreSlim to limit concurrency.
        var semaphore = new SemaphoreSlim(initialCount: MaxConcurrency, maxCount: MaxConcurrency);
        var semaphoreTimeout = TimeSpan.FromMinutes(60);

        // This queries all data sequentially, but that might not be as quick as we need.
        // TODO: How do we parallelize the query? What parameters can we use to split the query?
        await foreach (var queryResult in query.GetAsync(_databricksSqlWarehouseQueryExecutor).ConfigureAwait(false))
        {
            if (!queryResult.IsSuccess || queryResult.Result is null)
            {
                failedTransactions.Add(queryResult.Result?.TransactionId.ToString() ?? "Unknown transaction");
                continue;
            }

            var calculatedMeasurements = queryResult.Result;

            // Only start a new tasks if we can get the semaphore (if there are less than 100 tasks running)
            var didGetSemaphore = await semaphore.WaitAsync(semaphoreTimeout).ConfigureAwait(false);
            if (!didGetSemaphore)
            {
                _logger.LogError($"Failed to get semaphore within timeout (Timeout={semaphoreTimeout:g}).");
                failedTransactions.Add(calculatedMeasurements.TransactionId.ToString());
                continue;
            }

            // Start a new task to enqueue the measure data, but do not wait for it, since we want to handle
            // multiple tasks in parallel. The semaphore will ensure that we have maximum 100 tasks running at the same time.
            enqueueTasks.Add(
                Task.Run(async () =>
                    {
                        try
                        {
                            await EnqueueMessagesForMeasureDataAsync(
                                    input.OrchestrationInstanceId,
                                    calculatedMeasurements)
                                .ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(
                                e,
                                "Failed to enqueue measure data for transaction (TransactionId={TransactionId}, OrchestrationInstanceId={OrchestrationInstanceId}, MeteringPointId={MeteringPointId}).",
                                calculatedMeasurements.TransactionId,
                                input.OrchestrationInstanceId.Value,
                                calculatedMeasurements.MeteringPointId);
                            failedTransactions.Add(calculatedMeasurements.TransactionId.ToString());
                        }
                        finally
                        {
                            Interlocked.Increment(ref enqueuedTransactionsCount); // Increment count thread-safe
                            semaphore.Release();
                        }
                    }));
        }

        await Task.WhenAll(enqueueTasks).ConfigureAwait(false);

        if (!failedTransactions.IsEmpty)
            throw new Exception($"Failed to enqueue measure data for {failedTransactions.Count} transactions ({string.Join(", ", failedTransactions)}).");

        return enqueuedTransactionsCount;
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
        try
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
        catch (Exception e)
        {
            // We need to log the error here, because we want the transaction id to be part of the logged error message.
            _logger.LogError(
                e,
                "Failed to enqueue measure data for transaction (TransactionId={TransactionId}, OrchestrationInstanceId={OrchestrationInstanceId}, MeteringPointId={MeteringPointId}).",
                calculatedMeasureData.TransactionId,
                orchestrationInstanceId,
                calculatedMeasureData.MeteringPointId);

            throw new Exception(
                message: "Failed to enqueue measure data for transaction (TransactionId={TransactionId}, OrchestrationInstanceId={OrchestrationInstanceId}, MeteringPointId={MeteringPointId})",
                innerException: e);
        }
    }

    private Interval GetMeasurementsPeriod(CalculatedMeasurement calculatedMeasureData)
    {
        var from = calculatedMeasureData.MeasureData.First().ObservationTime;

        var resolutionAsDuration = calculatedMeasureData.Resolution switch
        {
            var r when r == Resolution.QuarterHourly => Duration.FromMinutes(15),
            var r when r == Resolution.Hourly => Duration.FromHours(1),
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
