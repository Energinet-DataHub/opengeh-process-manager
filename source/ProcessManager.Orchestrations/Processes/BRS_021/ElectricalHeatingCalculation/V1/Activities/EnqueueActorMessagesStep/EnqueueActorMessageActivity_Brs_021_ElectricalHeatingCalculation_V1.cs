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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Extensions;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;
using Microsoft.Azure.Functions.Worker;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Activities.EnqueueActorMessagesStep;

public class EnqueueActorMessageActivity_Brs_021_ElectricalHeatingCalculation_V1(
    MeteringPointMasterDataProvider meteringPointMasterDataProvider,
    MeteringPointReceiversProvider meteringPointReceiversProvider,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly MeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;
    private readonly MeteringPointReceiversProvider _meteringPointReceiversProvider = meteringPointReceiversProvider;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessageActivity_Brs_021_ElectricalHeatingCalculation_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        // Simulate getting a stream of data
        var dataStream = QueryDataAsync(input.CalculationPeriodStart, input.CalculationPeriodEnd);

        // The returned data stream must (in the end) be a list of
        string? currentMeteringPointId = null;
        List<ReceiversWithMeasureData.MeasureData> currentMeteredData = [];
        await foreach (var data in dataStream)
        {
            if (currentMeteringPointId == null)
                currentMeteringPointId = data.MeteringPointId;

            // Metering point id has changed, create a "packaged" message and enqueue to EDI.
            if (currentMeteringPointId != data.MeteringPointId)
            {
                // We need to get master data & receivers for each metering point id
                var masterDataForMeteringPoint = await _meteringPointMasterDataProvider.GetMasterData(
                        currentMeteringPointId,
                        input.CalculationPeriodStart,
                        input.CalculationPeriodEnd)
                    .ConfigureAwait(false);

                var receiversForMeteringPoint = _meteringPointReceiversProvider
                    .GetReceiversWithMeteredDataFromMasterDataList(
                        new MeteringPointReceiversProvider.FindReceiversInput(
                            data.MeteringPointId,
                            input.CalculationPeriodStart,
                            input.CalculationPeriodEnd,
                            data.Resolution,
                            masterDataForMeteringPoint,
                            currentMeteredData));

                // Enqueue to EDI
                // TODO: This probably needs to be handled differently, since this requires the orchestration to wait
                // for each enqueued messages event to be returned.
                await _enqueueActorMessagesClient.EnqueueAsync(
                        orchestration: Orchestration_Brs_021_ElectricalHeatingCalculation_V1.UniqueName,
                        orchestrationInstanceId: input.OrchestrationInstanceId.Value,
                        orchestrationStartedBy: new ActorIdentityDto(ActorNumber.Create("1234567890123"), ActorRole.GridAccessProvider),
                        // We need to create unique deterministic idempotency keys for each message sent to EDI instead,
                        // so rerunning the activity generates the same idempotency keys as previous run.
                        idempotencyKey: Guid.NewGuid(),
                        data: new EnqueueActorMessagesForMeteringPointV1(
                            ReceiversWithMeasureData: receiversForMeteringPoint.ToElectricalHeatingCalculationReceiversWithMeasureDataV1()))
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Simulate querying data rows from a data source.
    /// </summary>
    private async IAsyncEnumerable<
        (string MeteringPointId,
        Instant Timestamp,
        Resolution Resolution,
        int Quantity)>
        QueryDataAsync(Instant start, Instant end)
    {
        var meteringPointId1 = Guid.NewGuid().ToString();
        var meteringPointId2 = Guid.NewGuid().ToString();
        var meteringPointId3 = Guid.NewGuid().ToString();

        var resolution = Resolution.QuarterHourly;

        var now = start;

        var count = 0;
        while (now < end)
        {
            count++;

            var meteringPointId = count switch
            {
                > 10 => meteringPointId3,
                > 5 => meteringPointId2,
                _ => meteringPointId1,
            };

            var timestamp = now.Plus(Duration.FromMinutes(15)); // QuarterHourly resolution
            yield return (meteringPointId, timestamp, resolution, 42);

            now = timestamp;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        Instant CalculationPeriodStart,
        Instant CalculationPeriodEnd);
}
