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
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.Measurements.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Handlers;

public class StartForwardMeteredDataHandlerV1(
    ILogger<StartForwardMeteredDataHandlerV1> logger,
    IStartOrchestrationInstanceMessageCommands commands,
    IOrchestrationInstanceProgressRepository progressRepository,
    IClock clock,
    IMeasurementsMeteredDataClient measurementsMeteredDataClient)
        : StartOrchestrationInstanceFromMessageHandlerBase<ForwardMeteredDataInputV1>(logger)
{
    private readonly IStartOrchestrationInstanceMessageCommands _commands = commands;
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IClock _clock = clock;
    private readonly IMeasurementsMeteredDataClient _measurementsMeteredDataClient = measurementsMeteredDataClient;

    // TODO: This method is not idempotent, Since we can not set a "running" step to "running"
    // TODO: Hence we need to commit after the event/message has been sent
    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        ForwardMeteredDataInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        var orchestrationInstanceId = await _commands.StartNewOrchestrationInstanceAsync(
                actorIdentity,
                OrchestrationDescriptionBuilderV1.UniqueName.MapToDomain(),
                input,
                skipStepsBySequence: [],
                new IdempotencyKey(idempotencyKey),
                new ActorMessageId(actorMessageId),
                new TransactionId(transactionId),
                meteringPointId is not null ? new MeteringPointId(meteringPointId) : null)
            .ConfigureAwait(false);

        var orchestrationInstance = await _progressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Initialize orchestration instance
        if (orchestrationInstance.Lifecycle.State == OrchestrationInstanceLifecycleState.Queued)
        {
            orchestrationInstance.Lifecycle.TransitionToRunning(_clock);
            await _progressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);
        }

        if (orchestrationInstance.Lifecycle.State != OrchestrationInstanceLifecycleState.Running)
            return;

        // Start Step: Validate Metered Data
        var validationStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ValidationStep);
        await StepHelper.StartStep(validationStep, _clock, _progressRepository).ConfigureAwait(false);

        if (validationStep.Lifecycle.State == StepInstanceLifecycleState.Running)
        {
            // Fetch Metered Data and store received data used to find receiver later in the orchestration
            // Validate Metered Data

            // Terminate Step: Validate Metered Data
            await StepHelper.TerminateStep(validationStep, _clock, _progressRepository).ConfigureAwait(false);
        }

        // Start Step: Forward to Measurements
        var forwardToMeasurementStep = orchestrationInstance.GetStep(OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep);
        await StepHelper.StartStep(forwardToMeasurementStep, _clock, _progressRepository).ConfigureAwait(false);

        if (forwardToMeasurementStep.Lifecycle.State == StepInstanceLifecycleState.Running)
        {
            await _measurementsMeteredDataClient.SendAsync(
                    MapInputToMeasurements(orchestrationInstanceId, input),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static MeteredDataForMeteringPoint MapInputToMeasurements(
        OrchestrationInstanceId orchestrationInstanceId,
        ForwardMeteredDataInputV1 input) =>
        new(
            OrchestrationId: orchestrationInstanceId.Value.ToString(),
            MeteringPointId: input.MeteringPointId!,
            TransactionId: input.TransactionId,
            CreatedAt: InstantPattern.ExtendedIso.Parse(input.RegistrationDateTime).Value,
            StartDateTime: InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value,
            EndDateTime: InstantPatternWithOptionalSeconds.Parse(input.EndDateTime!).Value,
            MeteringPointType: MeteringPointType.FromName(input.MeteringPointType!),
            Unit: MeasurementUnit.FromName(input.MeasureUnit!),
            Resolution: Resolution.FromName(input.Resolution!),
            Points: input.EnergyObservations.Select(
                    MapPoints)
                .ToList());

    private static Point MapPoints(ForwardMeteredDataInputV1.EnergyObservation eo)
    {
        // TODO: temporary solution until we have business validation rules for quality
        var quality = string.IsNullOrWhiteSpace(eo.QuantityQuality) || eo.QuantityQuality == Quality.Incomplete.Name
            ? Quality.NotAvailable
            : Quality.FromName(eo.QuantityQuality);
        return new Point(
            int.Parse(eo.Position!),
            decimal.Parse(eo.EnergyQuantity!),
            quality);
    }
}
