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
    public static List<ReceiversWithMeasureDataV1> ToElectricalHeatingReceiversWithMeasureDataV1(
        this IEnumerable<ReceiversWithMeasureData> receiversWithMeasureData)
    {
        return receiversWithMeasureData
            .Select(
                rmd => new ReceiversWithMeasureDataV1(
                    Receivers: rmd.Receivers.ToElectricalHeatingReceivers(),
                    Resolution: rmd.Resolution,
                    MeasureUnit: rmd.MeasureUnit,
                    StartDateTime: rmd.StartDateTime,
                    EndDateTime: rmd.EndDateTime,
                    MeasureDataList: rmd.MeasureDataList.ToElectricalHeatingMeasureData()))
            .ToList();
    }

    private static List<ReceiversWithMeasureDataV1.Receiver> ToElectricalHeatingReceivers(
        this IEnumerable<ReceiversWithMeasureData.Receiver> receivers)
    {
        return receivers.Select(
                r => new ReceiversWithMeasureDataV1.Receiver(
                    ActorNumber: r.ActorNumber,
                    ActorRole: r.ActorRole))
            .ToList();
    }

    private static List<ReceiversWithMeasureDataV1.MeasureData> ToElectricalHeatingMeasureData(
        this IEnumerable<ReceiversWithMeasureData.MeasureData> measureData)
    {
        return measureData
            .Select(
                md => new ReceiversWithMeasureDataV1.MeasureData(
                    Position: md.Position,
                    EnergyQuantity: md.EnergyQuantity,
                    QuantityQuality: md.QuantityQuality))
            .ToList();
    }
}
