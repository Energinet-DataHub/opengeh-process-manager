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

using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Extensions;

public static class ReceiversWithMeasureDataExtensions
{
    public static List<ElectricalHeatingCalculationReceiversWithMeasureDataV1> ToElectricalHeatingCalculationReceiversWithMeasureDataV1(
        this IReadOnlyCollection<ReceiversWithMeasureData> receiversWithMeasureData)
    {
        return receiversWithMeasureData
            .Select(
                rmd => new ElectricalHeatingCalculationReceiversWithMeasureDataV1(
                    Actors: rmd.Receivers.ToReceivers(),
                    Resolution: rmd.Resolution,
                    MeasureUnit: rmd.MeasureUnit,
                    StartDateTime: rmd.StartDateTime,
                    EndDateTime: rmd.EndDateTime,
                    MeasureDataList: rmd.MeasureDataList.ToMeasureData()))
            .ToList();
    }

    private static IReadOnlyCollection<ElectricalHeatingCalculationReceiversWithMeasureDataV1.Receiver> ToReceivers(
        this IReadOnlyCollection<ReceiversWithMeasureData.ActorReceiver> receivers)
    {
        return receivers.Select(
                r => new ElectricalHeatingCalculationReceiversWithMeasureDataV1.Receiver(
                    ActorNumber: r.ActorNumber,
                    ActorRole: r.ActorRole))
            .ToList();
    }

    private static IReadOnlyCollection<ElectricalHeatingCalculationReceiversWithMeasureDataV1.MeasureData> ToMeasureData(
        this IReadOnlyCollection<ReceiversWithMeasureData.MeasureData> measuredata)
    {
        return measuredata
            .Select(
                md => new ElectricalHeatingCalculationReceiversWithMeasureDataV1.MeasureData(
                    Position: md.Position,
                    EnergyQuantity: md.EnergyQuantity!.Value, // TODO: Can this be null?
                    QuantityQuality: md.QuantityQuality!)) // TODO: Can this be null?
            .ToList();
    }
}
