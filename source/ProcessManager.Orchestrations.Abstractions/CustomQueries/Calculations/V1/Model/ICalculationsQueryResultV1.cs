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

using System.Text.Json.Serialization;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;

/// <summary>
/// Query result from searching for Calculations orchestration instances.
/// We use https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(ICalculationsQueryResultV1), typeDiscriminator: "base")]
[JsonDerivedType(typeof(WholesaleCalculationResultV1), typeDiscriminator: "wholesale")]
[JsonDerivedType(typeof(ElectricalHeatingCalculationResultV1), typeDiscriminator: "electricalheating")]
[JsonDerivedType(typeof(CapacitySettlementCalculationResultV1), typeDiscriminator: "capacitysettlement")]
[JsonDerivedType(typeof(NetConsumptionCalculationResultV1), typeDiscriminator: "netconsumption")]
public interface ICalculationsQueryResultV1;
