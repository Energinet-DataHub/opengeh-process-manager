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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;

/// <summary>
/// The input required to start a BRS-026 RequestCalculatedEnergyTimeSeries process.
/// </summary>
/// <param name="RequestedForActorNumber">GLN or EIC of the Actor for whom to retrieve data. Typically 13 og 16 numbers.</param>
/// <param name="RequestedForActorRole">The actor role of the Actor for whom to retrieve data. Eg: GridOperator, EnergySupplier etc.</param>
/// <param name="BusinessReason">The requested business reason. Eg: BalanceFixing, Correction etc.</param>
/// <param name="PeriodStart">The start of the requested period.</param>
/// <param name="PeriodEnd">Optional end of the requested period.</param>
/// <param name="EnergySupplierNumber">Optional GLN or EIC for the energy supplier.</param>
/// <param name="BalanceResponsibleNumber">Optional GLN or EIC for the balance responsible.</param>
/// <param name="GridAreas">Grid area codes to retreive data in. Eg: 870, 540 etc.</param>
/// <param name="MeteringPointType">Optional metering point types to retrieve data for. Eg: Consumption, Production etc.</param>
/// <param name="SettlementMethod">Optional settlement method to retrieve data for. Eg: NonProfiled, Flex etc.</param>
/// <param name="SettlementVersion">
/// Optional version of the requested correction. Eg: First-, Second- or ThirdCorrection.
/// Should only be set if BusinessReason is a Correction. Will find the latest correction if not provided.
/// </param>
public record RequestCalculatedEnergyTimeSeriesInputV1(
    string RequestedForActorNumber,
    string RequestedForActorRole,
    string BusinessReason,
    string PeriodStart,
    string? PeriodEnd,
    string? EnergySupplierNumber,
    string? BalanceResponsibleNumber,
    IReadOnlyCollection<string> GridAreas,
    string? MeteringPointType,
    string? SettlementMethod,
    string? SettlementVersion)
    : IInputParameterDto;
