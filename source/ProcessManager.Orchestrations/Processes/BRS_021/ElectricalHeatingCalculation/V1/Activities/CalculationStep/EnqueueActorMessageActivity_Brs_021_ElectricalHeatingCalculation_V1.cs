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

using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.CalculationStep;

public class EnqueueActorMessageActivity_Brs_021_ElectricalHeatingCalculation_V1(
    MeteringPointMasterDataProvider meteringPointMasterDataProvider,
    MeteringPointReceiversProvider meteringPointReceiversProvider)
{
    private readonly MeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;

    [Function(nameof(EnqueueActorMessageActivity_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        // Simulate getting a stream of data
        var dataStream = QueryData(input.CalculationPeriodStart, input.CalculationPeriodEnd);

        await foreach (var data in dataStream)
        {
            // We need to get master data & receivers for each metering point id
            var masterData = await _meteringPointMasterDataProvider.GetMasterData(
                    data.MeteringPointId,
                    input.CalculationPeriodStart,
                    input.CalculationPeriodEnd)
                .ConfigureAwait(false);

            var receiversForMasterData = _meteringPointReceiversProvider
                .GetReceiversWithMeteredDataFromMasterDataList(
                    new MeteringPointReceiversProvider.FindReceiversInput(
                        data.MeteringPointId,
                        input.CalculationPeriodStart,
                        input.CalculationPeriodEnd,
                        data.Resolution,
                        masterData,
                        data.MeasureData
                            .Select(
                                (md, i) => new ReceiversWithMeteredDataV1.AcceptedMeteredData(
                                    Position: i + 1,
                                    md.Quantity,
                                    md.Quality))
                            .ToList()));
        }
    }

    /// <summary>
    /// Simulate querying data rows from a data source.
    /// </summary>
    private async IAsyncEnumerable<
            (string MeteringPointId,
            Resolution Resolution,
            List<(Instant Timestamp, int Quantity, Quality Quality)> MeasureData)>
        QueryData(Instant start, Instant end)
    {
        var meteringPointId1 = Guid.NewGuid().ToString();
        var meteringPointId2 = Guid.NewGuid().ToString();
        var meteringPointId3 = Guid.NewGuid().ToString();

        var data = new List<(string MeteringPointId, Instant Timestamp, Resolution Resolution, int Quantity)>
        {
            (meteringPointId1, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId1, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId1, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId2, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId2, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId2, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId3, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId3, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId3, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
            (meteringPointId3, SystemClock.Instance.GetCurrentInstant(), Resolution.QuarterHourly, 42),
        };

        string? currentMeteringPointId = null;
        var currentDataRows = new List<(string MeteringPointId, Instant Timestamp, Resolution Resolution, int Quantity)>();
        foreach (var dataRow in data)
        {
            if (currentMeteringPointId == null)
                currentMeteringPointId = dataRow.MeteringPointId;

            if (currentMeteringPointId != dataRow.MeteringPointId)
            {
                yield return (
                    Guid.NewGuid().ToString(),
                    Resolution.QuarterHourly,
                    currentDataRows
                        .Select(d => (d.Timestamp, d.Quantity, Quality.Calculated))
                        .ToList());
                currentDataRows = [];
                currentMeteringPointId = dataRow.MeteringPointId;
            }

            currentDataRows.Add(dataRow);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public record ActivityInput(
        Instant CalculationPeriodStart,
        Instant CalculationPeriodEnd);
}
