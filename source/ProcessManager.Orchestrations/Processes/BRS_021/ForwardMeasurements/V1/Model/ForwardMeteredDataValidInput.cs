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
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.Shared.ElectricityMarket.Extensions;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeasurements.V1.Model;

/// <summary>
/// A representation of a valid input for the Forward Metered Data process.
/// </summary>
public record ForwardMeteredDataValidInput(
    ActorMessageId ActorMessageId,
    TransactionId TransactionId,
    ActorNumber ActorNumber,
    ActorRole ActorRole,
    BusinessReason BusinessReason,
    MeteringPointId MeteringPointId,
    MeteringPointType MeteringPointType,
    string? ProductNumber,
    MeasurementUnit MeasureUnit,
    Instant RegistrationDateTime,
    Resolution Resolution,
    Instant StartDateTime,
    Instant EndDateTime,
    ActorNumber GridAccessProvider,
    IReadOnlyCollection<ForwardMeteredDataValidInput.MeteredData> MeteredDataList)
    : IInputParameterDto
{
    public static ForwardMeteredDataValidInput From(ForwardMeteredDataInputV1 input)
    {
        var meteredDataList = input.MeteredDataList
            .Select(e => new MeteredData(
                Position: int.Parse(e.Position!),
                EnergyQuantity: e.EnergyQuantity != null ? decimal.Parse(e.EnergyQuantity, NumberFormatInfo.InvariantInfo) : null,
                QuantityQuality: e.QuantityQuality is null ? Quality.AsProvided : Quality.FromName(e.QuantityQuality!)))
            .ToList();

        var startDateTime = InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value;
        var endDateTime = InstantPatternWithOptionalSeconds.Parse(input.EndDateTime!).Value;
        var registrationDateTime = InstantPatternWithOptionalSeconds.Parse(input.RegistrationDateTime).Value;

        return new ForwardMeteredDataValidInput(
            ActorMessageId: new ActorMessageId(input.ActorMessageId),
            TransactionId: new TransactionId(input.TransactionId),
            ActorNumber: ActorNumber.Create(input.ActorNumber),
            ActorRole: ActorRole.FromName(input.ActorRole),
            BusinessReason: BusinessReason.FromName(input.BusinessReason),
            MeteringPointId: new MeteringPointId(input.MeteringPointId!),
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            ProductNumber: input.ProductNumber,
            MeasureUnit: MeasurementUnit.FromName(input.MeasureUnit!),
            RegistrationDateTime: registrationDateTime,
            Resolution: Resolution.FromName(input.Resolution!),
            StartDateTime: startDateTime,
            EndDateTime: endDateTime,
            GridAccessProvider: ActorNumber.Create(input.GridAccessProviderNumber),
            MeteredDataList: meteredDataList);
    }

    public record MeteredData(
        int Position,
        decimal? EnergyQuantity,
        Quality QuantityQuality);
}
