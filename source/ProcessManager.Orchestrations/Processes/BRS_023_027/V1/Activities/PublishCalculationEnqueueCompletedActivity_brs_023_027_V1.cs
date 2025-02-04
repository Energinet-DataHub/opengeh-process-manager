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

using Energinet.DataHub.Brs023027.Contracts;
using Energinet.DataHub.ProcessManager.Components.IntegrationEventPublisher;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Microsoft.Azure.Functions.Worker;
using CalculationType = Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model.CalculationType;
using Proto = Energinet.DataHub.Brs023027.Contracts;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_023_027.V1.Activities;

internal class PublishCalculationEnqueueCompletedActivity_brs_023_027_V1(
    IOrchestrationInstanceProgressRepository repository,
    IIntegrationEventPublisherClient integrationEventPublisherClient)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;
    private readonly IIntegrationEventPublisherClient _integrationEventPublisherClient = integrationEventPublisherClient;

    [Function(nameof(PublishCalculationEnqueueCompletedActivity_brs_023_027_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<CalculationInputV1>();
        var integrationEvent = new CalculationEnqueueCompletedV1()
        {
            CalculationId = input.CalculationId.ToString(),
            CalculationType = Map(orchestrationInstanceInput.CalculationType),
        };

        await _integrationEventPublisherClient.PublishAsync(
            eventIdentification: input.IdempotencyKey,
            eventName: CalculationEnqueueCompletedV1.Descriptor.Name,
            eventMinorVersion: 1,
            message: integrationEvent,
            CancellationToken.None).ConfigureAwait(false);
    }

    private Proto.CalculationType Map(CalculationType calculationType)
    {
        return calculationType switch {
            CalculationType.BalanceFixing => Proto.CalculationType.BalanceFixing,
            CalculationType.Aggregation => Proto.CalculationType.Aggregation,
            CalculationType.WholesaleFixing => Proto.CalculationType.WholesaleFixing,
            CalculationType.FirstCorrectionSettlement => Proto.CalculationType.FirstCorrectionSettlement,
            CalculationType.SecondCorrectionSettlement => Proto.CalculationType.SecondCorrectionSettlement,
            CalculationType.ThirdCorrectionSettlement => Proto.CalculationType.ThirdCorrectionSettlement,
            _ => throw new ArgumentOutOfRangeException(
                nameof(calculationType),
                calculationType,
                null),
        };
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        Guid CalculationId,
        Guid IdempotencyKey);
}
