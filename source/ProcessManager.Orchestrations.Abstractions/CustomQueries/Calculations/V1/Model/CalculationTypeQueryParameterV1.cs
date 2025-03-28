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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;

public enum CalculationTypeQueryParameterV1
{
    /// <summary>
    /// BRS-023/027 Balance fixing
    /// </summary>
    BalanceFixing = 0,

    /// <summary>
    /// BRS-023/027 Aggregation.
    /// </summary>
    Aggregation = 1,

    /// <summary>
    /// BRS-023/027 Wholesale fixing.
    /// </summary>
    WholesaleFixing = 2,

    /// <summary>
    /// BRS-023/027 First correction settlement.
    /// </summary>
    FirstCorrectionSettlement = 3,

    /// <summary>
    /// BRS-023/027 Second correction settlement.
    /// </summary>
    SecondCorrectionSettlement = 4,

    /// <summary>
    /// BRS-023/027 Third correction settlement.
    /// </summary>
    ThirdCorrectionSettlement = 5,

    /// <summary>
    /// BRS-021 Eletrical heating (Elvarme)
    /// </summary>
    ElectricalHeating = 6,

    /// <summary>
    /// BRS-021 Capacity settlement (Effektbetaling)
    /// </summary>
    CapacitySettlement = 7,

    /// <summary>
    /// BRS-021 Net Consumption (Nettoforbrug gruppe 6)
    /// </summary>
    NetConsumption = 8,
}
