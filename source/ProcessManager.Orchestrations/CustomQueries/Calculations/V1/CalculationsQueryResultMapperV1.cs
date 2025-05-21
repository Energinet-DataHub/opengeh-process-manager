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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.CapacitySettlementCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ElectricalHeatingCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.NetConsumptionCalculation;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal static class CalculationsQueryResultMapperV1
{
    public static IReadOnlyCollection<string> SupportedOrchestrationDescriptionNames { get; } = [
        Brs_023_027.Name,
        Brs_021_ElectricalHeatingCalculation.Name,
        Brs_021_CapacitySettlementCalculation.Name,
        Brs_021_NetConsumptionCalculation.Name,
        Brs_045_MissingMeasurementsLogCalculation.Name,
    ];

    /// <summary>
    /// Map from an orchestration instance to a concrete result DTO.
    /// Possible DTO types are specified by attributes on <see cref="ICalculationsQueryResultV1"/>.
    /// </summary>
    /// <param name="uniqueName">The orchestration description unique name of <paramref name="orchestrationInstance"/>.</param>
    /// <param name="orchestrationInstance"></param>
    /// <returns>A concrete calculation result DTO.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="uniqueName"/> does not match any of the supported result types.
    /// </exception>
    public static ICalculationsQueryResultV1 MapToDto(
        OrchestrationDescriptionUniqueName uniqueName,
        OrchestrationInstance orchestrationInstance)
    {
        switch (uniqueName.Name)
        {
            case Brs_023_027.Name:
                var wholesale = orchestrationInstance.MapToTypedDto<Abstractions.Processes.BRS_023_027.V1.Model.CalculationInputV1>();
                return new WholesaleCalculationResultV1(
                    wholesale.Id,
                    wholesale.Lifecycle,
                    wholesale.Steps,
                    wholesale.CustomState,
                    wholesale.ParameterValue);

            case Brs_021_ElectricalHeatingCalculation.Name:
                var electricalHeating = orchestrationInstance.MapToDto();
                return new ElectricalHeatingCalculationResultV1(
                    electricalHeating.Id,
                    electricalHeating.Lifecycle,
                    electricalHeating.Steps,
                    electricalHeating.CustomState);

            case Brs_021_CapacitySettlementCalculation.Name:
                var capacitySettlement = orchestrationInstance.MapToTypedDto<Abstractions.Processes.BRS_021.CapacitySettlementCalculation.V1.Model.CalculationInputV1>();
                return new CapacitySettlementCalculationResultV1(
                    capacitySettlement.Id,
                    capacitySettlement.Lifecycle,
                    capacitySettlement.Steps,
                    capacitySettlement.CustomState,
                    capacitySettlement.ParameterValue);

            case Brs_021_NetConsumptionCalculation.Name:
                var netConsumption = orchestrationInstance.MapToDto();
                return new NetConsumptionCalculationResultV1(
                    netConsumption.Id,
                    netConsumption.Lifecycle,
                    netConsumption.Steps,
                    netConsumption.CustomState);

            case Brs_045_MissingMeasurementsLogCalculation.Name:
                var missingMeasurementsLog = orchestrationInstance.MapToDto();
                return new MissingMeasurementsLogCalculationResultV1(
                    missingMeasurementsLog.Id,
                    missingMeasurementsLog.Lifecycle,
                    missingMeasurementsLog.Steps,
                    missingMeasurementsLog.CustomState);

            default:
                throw new InvalidOperationException($"Unsupported unique name '{uniqueName.Name}'.");
        }
    }
}
