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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.BusinessValidation;

public class MeasurementUnitValidationRule
    : IBusinessValidationRule<ForwardMeteredDataBusinessValidatedDto>
{
    public static IList<ValidationError> MeasurementUnitError => [new(
        Message: "Energienhed skal svare til energienhed på målepunktet/Measure unit must be the same as the one registrered on the meteringpoint",
        ErrorCode: "E73")];

    public static IList<ValidationError> MeasurementUnitNotAllowedError => [new(
        Message: "Energienhed skal være KWH eller Kvarh/Measure unit must be KWH or Kvarh",
        ErrorCode: "E73")];

    private static IList<ValidationError> NoError => [];

    private static IReadOnlyCollection<MeasurementUnit> AllowedMeasurementUnits => new[]
    {
        MeasurementUnit.KilowattHour,
        MeasurementUnit.KiloVoltAmpereReactiveHour,
    };

    public Task<IList<ValidationError>> ValidateAsync(
        ForwardMeteredDataBusinessValidatedDto subject)
    {
        if (subject.HistoricalMeteringPointMasterData.Count == 0)
        {
            return Task.FromResult(NoError);
        }

        var incomingMeasurementUnit = MeasurementUnit.FromNameOrDefault(subject.Input.MeasureUnit);
        if (!AllowedMeasurementUnits.Contains(incomingMeasurementUnit))
        {
            return Task.FromResult(MeasurementUnitError);
        }

        // Check if the measure unit is same for all historic master data
        if (subject.HistoricalMeteringPointMasterData
            .Select(mpmd => mpmd.MeasurementUnit)
            .Any(meteringPointType => meteringPointType != incomingMeasurementUnit))
        {
            return Task.FromResult(MeasurementUnitNotAllowedError);
        }

        return Task.FromResult(NoError);
    }
}
