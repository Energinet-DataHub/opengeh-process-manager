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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.NoInputExample;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal static class ExamplesQueryResultMapperV1
{
    public static IReadOnlyCollection<string> SupportedOrchestrationDescriptionNames { get; } = [
        Brs_X01_InputExample.Name,
        Brs_X01_NoInputExample.Name];

    /// <summary>
    /// Map from an orchestration instance to a concrete result DTO.
    /// Possible DTO types are specified by attributes on <see cref="IExamplesQueryResultV1"/>.
    /// </summary>
    /// <param name="uniqueName">The orchestration description unique name of <paramref name="orchestrationInstance"/>.</param>
    /// <param name="orchestrationInstance"></param>
    /// <returns>A concrete examples result DTO.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="uniqueName"/> does not match any of the supported result types.
    /// </exception>
    public static IExamplesQueryResultV1 MapToDto(
        OrchestrationDescriptionUniqueName uniqueName,
        OrchestrationInstance orchestrationInstance)
    {
        switch (uniqueName.Name)
        {
            case Brs_X01_InputExample.Name:
                var inputExample = orchestrationInstance.MapToTypedDto<InputV1>();
                return new InputExampleResultV1(
                    inputExample.Id,
                    inputExample.Lifecycle,
                    inputExample.Steps,
                    inputExample.CustomState,
                    inputExample.ParameterValue);

            case Brs_X01_NoInputExample.Name:
                var noInputExample = orchestrationInstance.MapToDto();
                return new NoInputExampleResultV1(
                    noInputExample.Id,
                    noInputExample.Lifecycle,
                    noInputExample.Steps,
                    noInputExample.CustomState);

            default:
                throw new InvalidOperationException($"Unsupported unique name '{uniqueName.Name}'.");
        }
    }
}
