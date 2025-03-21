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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_045.MissingMeasurementsLogOnDemandCalculation.V1;

internal class StartCalculationHandlerV1(
    IStartOrchestrationInstanceCommands manager) :
        IStartOrchestrationInstanceCommandHandler<StartCalculationCommandV1, CalculationInputV1>
{
    public async Task<Guid> HandleAsync(StartCalculationCommandV1 command)
    {
        GuardInputParameter(command.InputParameter);

        var orchestrationInstanceId = await manager
            .StartNewOrchestrationInstanceAsync(
                identity: command.OperatingIdentity.MapToDomain(),
                uniqueName: command.OrchestrationDescriptionUniqueName.MapToDomain(),
                inputParameter: command.InputParameter,
                skipStepsBySequence: [])
            .ConfigureAwait(false);

        return orchestrationInstanceId.Value;
    }

    /// <summary>
    /// Validate if input parameters are valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">If parameter input is not valid and exception is thrown that
    /// contains validation errors in its message property.</exception>
    private void GuardInputParameter(CalculationInputV1 inputParameter)
    {
        var period = inputParameter.PeriodEnd - inputParameter.PeriodStart;
        if (period.Days is > 31 or < 1)
        {
            throw new InvalidOperationException("The period interval must be between 0 and 31 days.");
        }

        if (inputParameter.GridAreaCodes.Count == 0)
        {
            throw new InvalidOperationException("At least one grid area code must be provided.");
        }
    }
}
