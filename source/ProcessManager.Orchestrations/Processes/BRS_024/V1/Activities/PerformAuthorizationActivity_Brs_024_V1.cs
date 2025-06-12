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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Authorization;
using Energinet.DataHub.ProcessManager.Components.Authorization.Model;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration.Steps;
using Microsoft.Azure.Functions.Worker;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.Authorization.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Activities;

public class PerformAuthorizationActivity_Brs_024_V1(
    IOrchestrationInstanceProgressRepository repository,
    IAuthorization authorization)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IAuthorization _authorization = authorization;

    [Function(nameof(PerformAuthorizationActivity_Brs_024_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestYearlyMeasurementsInputV1>();

        var validatedPeriods = await _authorization
            .GetAuthorizedPeriodsAsync(
                actorNumber: ActorNumber.Create(orchestrationInstanceInput.ActorNumber),
                actorRole: ActorRole.FromName(orchestrationInstanceInput.ActorRole),
                meteringPointId: new MeteringPointId(orchestrationInstanceInput.MeteringPointId),
                requestedPeriod: new RequestedPeriod())
            .ConfigureAwait(false);

        var isAuthorization = validatedPeriods.Count == 0;

        if (!isAuthorization)
        {
            var step = orchestrationInstance.GetStep(input.StepSequence);
            step.CustomState.SetFromInstance(new AuthorizationStep.CustomState(
                IsValid: false));
            await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        int StepSequence);

    public record ActivityOutput(
        bool IsAuthorized,
        IReadOnlyCollection<AuthorizedPeriod> ValidatedPeriod);
}
