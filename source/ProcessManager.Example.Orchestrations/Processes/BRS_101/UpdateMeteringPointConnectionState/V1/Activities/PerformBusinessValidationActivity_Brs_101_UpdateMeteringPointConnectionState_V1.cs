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

using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Steps;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_101.UpdateMeteringPointConnectionState.V1.Activities;

/// <summary>
/// Perform business validation.
/// </summary>
internal class PerformBusinessValidationActivity_Brs_101_UpdateMeteringPointConnectionState_V1(
    IOrchestrationInstanceProgressRepository repository,
    BusinessValidator<UpdateMeteringPointConnectionStateInputV1> validator)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly BusinessValidator<UpdateMeteringPointConnectionStateInputV1> _validator = validator;

    [Function(nameof(PerformBusinessValidationActivity_Brs_101_UpdateMeteringPointConnectionState_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<UpdateMeteringPointConnectionStateInputV1>();
        var validationErrors = await _validator
            .ValidateAsync(orchestrationInstanceInput)
            .ConfigureAwait(false);

        if (validationErrors.Count > 0)
        {
            var step = orchestrationInstance.GetStep(BusinessValidationStep.StepSequence);
            step.CustomState.SetFromInstance(new BusinessValidationStep.CustomState(
                IsValid: validationErrors.Count == 0,
                ValidationErrors: validationErrors));

            await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        return new ActivityOutput(
            IsValid: validationErrors.Count == 0,
            ValidationErrors: validationErrors);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);

    public record ActivityOutput(
        bool IsValid,
        IReadOnlyCollection<ValidationError> ValidationErrors);
}
