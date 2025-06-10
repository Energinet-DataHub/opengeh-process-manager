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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Orchestration.Steps;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Activities;

internal class PerformBusinessValidationActivity_Brs_025_V1(
    IOrchestrationInstanceProgressRepository repository,
    BusinessValidator<RequestMeasurementsBusinessValidatedDto> validator,
    IMeteringPointMasterDataProvider meteringPointMasterDataProvider)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly BusinessValidator<RequestMeasurementsBusinessValidatedDto> _validator = validator;
    private readonly IMeteringPointMasterDataProvider _meteringPointMasterDataProvider = meteringPointMasterDataProvider;

    [Function(nameof(PerformBusinessValidationActivity_Brs_025_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestMeasurementsInputV1>();

        var meteringPointMasterData =
            await _meteringPointMasterDataProvider
                .GetMasterData(
                    orchestrationInstanceInput.MeteringPointId,
                    orchestrationInstanceInput.StartDateTime,
                    orchestrationInstanceInput.EndDateTime!)
                .ConfigureAwait(false);

        var validationErrors = await _validator
            .ValidateAsync(new RequestMeasurementsBusinessValidatedDto(
                    Input: orchestrationInstanceInput,
                    HistoricalMeteringPointMasterData: meteringPointMasterData))
            .ConfigureAwait(false);

        var isValid = validationErrors.Count == 0;
        if (!isValid)
        {
            var step = orchestrationInstance.GetStep(input.StepSequence);
            step.CustomState.SetFromInstance(new BusinessValidationStep.CustomState(
                IsValid: false,
                ValidationErrors: validationErrors));
            await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        return new ActivityOutput(
            IsValid: isValid,
            ValidationErrors: validationErrors);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        int StepSequence);

    public record ActivityOutput(
        bool IsValid,
        IReadOnlyCollection<ValidationError> ValidationErrors);
}
