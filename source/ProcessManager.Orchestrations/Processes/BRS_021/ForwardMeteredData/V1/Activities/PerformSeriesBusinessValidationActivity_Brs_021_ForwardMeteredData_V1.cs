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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal class PerformSeriesBusinessValidationActivity_Brs_021_ForwardMeteredData_V1(
    IOrchestrationInstanceProgressRepository repository,
    BusinessValidator<Brs021_ForwardMeteredData_Series_BusinessValidationDto> validator)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly BusinessValidator<Brs021_ForwardMeteredData_Series_BusinessValidationDto> _validator = validator;

    [Function(nameof(PerformSeriesBusinessValidationActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task<ActivityOutput> Run([ActivityTrigger] ActivityInput activityInput)
    {
        var validationErrors = await _validator
            .ValidateAsync(new(activityInput.RequestInput, activityInput.MeteringPointMasterData))
            .ConfigureAwait(false);

        var activityOutput = new ActivityOutput(validationErrors);

        if (validationErrors.Count > 0)
        {
            var orchestrationInstance =
                await _repository.GetAsync(activityInput.OrchestrationInstanceId).ConfigureAwait(false);

            var step = orchestrationInstance.GetStep(activityInput.StepSequence);
            step.SetCustomState(JsonSerializer.Serialize(activityOutput));
            await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        return activityOutput;
    }

    public sealed record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        MeteredDataForMeteringPointMessageInputV1 RequestInput,
        int StepSequence,
        IReadOnlyCollection<MeteringPointMasterData> MeteringPointMasterData);

    public sealed record ActivityOutput(IReadOnlyCollection<ValidationError> ValidationErrors);
}
