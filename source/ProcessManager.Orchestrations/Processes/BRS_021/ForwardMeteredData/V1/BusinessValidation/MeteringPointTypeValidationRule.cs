﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class MeteringPointTypeValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> WrongMeteringPointError => [new(
        Message: "Forkert Målepunkts type/Wrong meteringpoint type",
        ErrorCode: "D18")];

    private static IList<ValidationError> NoError => [];

    private static IReadOnlyCollection<MeteringPointType> AllowedMeteringPointTypes => new[]
    {
        // Parent Metering Point Types
        MeteringPointType.Production,
        MeteringPointType.Consumption,
        MeteringPointType.Exchange,

        // Child Metering Point Types
        MeteringPointType.VeProduction,
        MeteringPointType.Analysis,
        MeteringPointType.NotUsed,
        MeteringPointType.SurplusProductionGroup6,
        MeteringPointType.NetProduction,
        MeteringPointType.SupplyToGrid,
        MeteringPointType.ConsumptionFromGrid,
        MeteringPointType.WholesaleServicesInformation,
        MeteringPointType.OwnProduction,
        MeteringPointType.NetFromGrid,
        MeteringPointType.NetToGrid,
        MeteringPointType.TotalConsumption,
        MeteringPointType.NetLossCorrection,
        MeteringPointType.ElectricalHeating,
        MeteringPointType.NetConsumption,
        MeteringPointType.OtherConsumption,
        MeteringPointType.OtherProduction,
        MeteringPointType.CapacitySettlement,
        MeteringPointType.ExchangeReactiveEnergy,
        MeteringPointType.CollectiveNetProduction,
        MeteringPointType.CollectiveNetConsumption,
        MeteringPointType.InternalUse,
    };

    public Task<IList<ValidationError>> ValidateAsync(ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (subject.MeteringPointMasterData.Count == 0)
        {
            return Task.FromResult(NoError);
        }

        var incomingMeteringPointType = MeteringPointType.FromNameOrDefault(subject.Input.MeteringPointType);
        if (!AllowedMeteringPointTypes.Contains(incomingMeteringPointType))
        {
            return Task.FromResult(WrongMeteringPointError);
        }

        // Check if the metering point type is same for all historic master data
        if (subject.MeteringPointMasterData
            .Select(mpmd => mpmd.MeteringPointType)
            .Any(meteringPointType => meteringPointType != incomingMeteringPointType))
        {
            return Task.FromResult(WrongMeteringPointError);
        }

        return Task.FromResult(NoError);
    }
}
