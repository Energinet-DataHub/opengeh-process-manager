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
        : StartOrchestrationInstanceFromMessageHandlerBase<MeteredDataForMeteringPointMessageInputV1>(logger)
{
    private IOrchestrationInstanceProgressRepository ProgressRepository { get; } = progressRepository;

    private IMeasurementsMeteredDataClient MeasurementsMeteredDataClient { get; } = measurementsMeteredDataClient;

    // TODO: This method is not idempotent, Since we can not set a "running" step to "running"
    // TODO: Hence we need to commit after the event/message has been sent
    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        MeteredDataForMeteringPointMessageInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        var orchestrationInstanceId = await commands.StartNewOrchestrationInstanceAsync(
                actorIdentity,
                OrchestrationDescriptionBuilderV1.UniqueName.MapToDomain(),
                input,
                skipStepsBySequence: [],
                new IdempotencyKey(idempotencyKey),
                new ActorMessageId(actorMessageId),
                new TransactionId(transactionId),
                meteringPointId is not null ? new MeteringPointId(meteringPointId) : null)
            .ConfigureAwait(false);

        var orchestrationInstance = await ProgressRepository
            .GetAsync(orchestrationInstanceId)
            .ConfigureAwait(false);

        // Initialize orchestration instance
        orchestrationInstance.Lifecycle.TransitionToRunning(clock);
        await ProgressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        // Start Step: Validate Metered Data
        await StepHelper.StartStep(orchestrationInstance, OrchestrationDescriptionBuilderV1.ValidationStep, clock, ProgressRepository).ConfigureAwait(false);

        // Fetch Metered Data and store received data used to find receiver later in the orchestration
        // Validate Metered Data
        // Terminate Step: Validate Metered Data
        await StepHelper.TerminateStep(orchestrationInstance, OrchestrationDescriptionBuilderV1.ValidationStep, clock, ProgressRepository).ConfigureAwait(false);

        // Start Step: Forward to Measurements
        await StepHelper.StartStep(orchestrationInstance, OrchestrationDescriptionBuilderV1.ForwardToMeasurementStep, clock, ProgressRepository).ConfigureAwait(false);

        // TODO: Do we want to inform our own trigger instead of measurement?
        await MeasurementsMeteredDataClient.SendAsync(
            GenerateMeteredData(orchestrationInstanceId, input),
            CancellationToken.None).ConfigureAwait(false);
    }

#pragma warning disable SA1202
    public static MeteredDataForMeteringPoint GenerateMeteredData(
        OrchestrationInstanceId orchestrationInstanceId,
        MeteredDataForMeteringPointMessageInputV1 input)
    {
        return new MeteredDataForMeteringPoint(
            OrchestrationId: orchestrationInstanceId.Value.ToString(),
            MeteringPointId: input.MeteringPointId!,
            TransactionId: input.TransactionId,
            CreatedAt: InstantPattern.ExtendedIso.Parse(input.RegistrationDateTime).Value,
            StartDateTime: InstantPatternWithOptionalSeconds.Parse(input.StartDateTime).Value,
            EndDateTime: InstantPatternWithOptionalSeconds.Parse(input.EndDateTime!).Value,
            MeteringPointType: MeteringPointType.FromName(MeteringPointType.Production.Name),
            Product: input.ProductNumber ?? string.Empty,
            Unit: MeasurementUnit.FromName(input.MeasureUnit ?? MeasurementUnit.Megawatt.Name),
            Resolution: Resolution.FromName(input.Resolution ?? Resolution.Hourly.Name),
            Points: []);
    }
#pragma warning restore SA1202
}
