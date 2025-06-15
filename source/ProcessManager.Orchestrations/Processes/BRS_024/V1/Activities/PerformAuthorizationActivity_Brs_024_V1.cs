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
using Energinet.DataHub.ProcessManager.Components.BusinessValidation;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration.Steps;
using Microsoft.Azure.Functions.Worker;
using NodaTime.Text;
using MeteringPointId = Energinet.DataHub.ProcessManager.Components.Authorization.Model.MeteringPointId;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Activities;

public class PerformAuthorizationActivity_Brs_024_V1(
    IOrchestrationInstanceProgressRepository repository,
    IAuthorization authorization)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IAuthorization _authorization = authorization;
    private readonly string _validationErrorText = "Forespørgslen afvises da I ikke er legitim aktør ift. "
                                                   + "målepunkt eller periode. "
                                                   + "Det kan skyldes, delegering, ejerskab af netområde eller i ikke må spørge på perioden/"
                                                   + "The request is denied as you are not an authorized actor for the metering point or the period, "
                                                   + "it can be because of delegation, ownership of grid area or not permitted to request data for the period.";

    [Function(nameof(PerformAuthorizationActivity_Brs_024_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestYearlyMeasurementsInputV1>();

        var receivedAt = InstantPattern.General.Parse(orchestrationInstanceInput.ReceivedAt).Value;

        var validatedPeriods = await _authorization
            .GetAuthorizedPeriodsAsync(
                actorNumber: ActorNumber.Create(orchestrationInstanceInput.ActorNumber),
                actorRole: ActorRole.FromName(orchestrationInstanceInput.ActorRole),
                meteringPointId: new MeteringPointId(orchestrationInstanceInput.MeteringPointId),
                requestedPeriod: new RequestedPeriod(
                    Start: receivedAt.ToDateTimeOffset(), // TODO: Update these values
                    End: receivedAt.ToDateTimeOffset()))
            .ConfigureAwait(false);

        var isAuthorization = validatedPeriods.Count == 0;

        var validationsErrors = new List<ValidationError>();

        if (!isAuthorization)
        {
            validationsErrors.Add(
                new ValidationError(
                    ErrorCode: "D44",
                    Message: _validationErrorText));

            var step = orchestrationInstance.GetStep(input.StepSequence);
            step.CustomState.SetFromInstance(new AuthorizationStep.CustomState(
                IsValid: false,
                ValidationErrors: validationsErrors));
            await _repository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        return new ActivityOutput(
            IsAuthorized: isAuthorization,
            ValidatedPeriod: validatedPeriods,
            ValidationsErrors: validationsErrors);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        int StepSequence);

    public record ActivityOutput(
        bool IsAuthorized,
        IReadOnlyCollection<AuthorizedPeriod> ValidatedPeriod,
        IReadOnlyCollection<ValidationError> ValidationsErrors);
}
