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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.Extensions;

public static class ReceiversWithMeasurementsExtensions
{
    public static List<ReceiversWithMeteredDataV1> ToForwardMeteredDataReceiversWithMeasurementsV1(
        this IEnumerable<ReceiversWithMeasurements> receiversWithMeasurements)
    {
        return receiversWithMeasurements
            .Select(
                rmd => new ReceiversWithMeteredDataV1(
                    Actors: rmd.Receivers.ToForwardMeteredDataMarketActorRecipientV1(),
                    Resolution: rmd.Resolution,
                    MeasureUnit: rmd.MeasureUnit,
                    StartDateTime: rmd.StartDateTime,
                    EndDateTime: rmd.EndDateTime,
                    MeteredData: rmd.Measurements.ToForwardMeteredDataAcceptedMeasurements()))
            .ToList();
    }

    private static List<MarketActorRecipientV1> ToForwardMeteredDataMarketActorRecipientV1(
        this IEnumerable<Actor> receivers)
    {
        return receivers.Select(
                r => new MarketActorRecipientV1(
                    ActorNumber: r.Number,
                    ActorRole: r.Role))
            .ToList();
    }

    private static List<ReceiversWithMeteredDataV1.AcceptedMeteredData> ToForwardMeteredDataAcceptedMeasurements(
        this IEnumerable<ReceiversWithMeasurements.Measurement> measurements)
    {
        return measurements
            .Select(
                md => new ReceiversWithMeteredDataV1.AcceptedMeteredData(
                    Position: md.Position,
                    EnergyQuantity: md.EnergyQuantity,
                    QuantityQuality: md.QuantityQuality))
            .ToList();
    }
}
