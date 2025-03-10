﻿// Copyright 2020 Energinet DataHub A/S
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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using MeteringPointType = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.MeteringPointType;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal class EnqueueActorMessagesActivity_Brs_021_ForwardMeteredData_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput activityInput)
    {
        var orchestrationInstance = await ProgressRepository
            .GetAsync(activityInput.OrchestrationInstanceId)
            .ConfigureAwait(false);

        await TransitionStepToRunningAsync(
                OrchestrationDescriptionBuilderV1.EnqueueActorMessagesStep,
                orchestrationInstance)
            .ConfigureAwait(false);

        await ProgressRepository.UnitOfWork.CommitAsync().ConfigureAwait(false);

        var messageInput = activityInput.ForwardMeteredDataInputV1;

        var acceptedEnergyObservations = messageInput.EnergyObservations
            .Select(
                x => new ForwardMeteredDataAcceptedV1.AcceptedEnergyObservation(
                    int.Parse(x.Position!),
                    decimal.Parse(x.EnergyQuantity!),
                    Quality.FromName(x.QuantityQuality!)))
            .ToList();

        var receiver = activityInput.ForwardMeteredDataInputV1.TransactionId.Contains("perf_test")
            ? new MarketActorRecipientV1(ActorNumber.Create("8100000000115"), ActorRole.EnergySupplier)
            : new MarketActorRecipientV1(ActorNumber.Create("5790000282425"), ActorRole.EnergySupplier);

        var data = new ForwardMeteredDataAcceptedV1(
            OriginalActorMessageId: messageInput.MessageId,
            OriginalTransactionId: activityInput.ForwardMeteredDataInputV1.TransactionId,
            MeteringPointId: messageInput.MeteringPointId!,
            MeteringPointType: MeteringPointType.FromName(messageInput.MeteringPointType!),
            ProductNumber: messageInput.ProductNumber!,
            MeasureUnit: MeasurementUnit.FromName(messageInput.MeasureUnit!),
            RegistrationDateTime: InstantPatternWithOptionalSeconds.Parse(messageInput.RegistrationDateTime).Value.ToDateTimeOffset(),
            Resolution: Resolution.FromName(messageInput.Resolution!),
            StartDateTime: InstantPatternWithOptionalSeconds.Parse(messageInput.StartDateTime).Value.ToDateTimeOffset(),
            EndDateTime: InstantPatternWithOptionalSeconds.Parse(messageInput.EndDateTime!).Value.ToDateTimeOffset(),
            AcceptedEnergyObservations: acceptedEnergyObservations,
            MarketActorRecipients: [receiver]);

        await _enqueueActorMessagesClient.EnqueueAsync(
            OrchestrationDescriptionBuilderV1.UniqueName,
            activityInput.OrchestrationInstanceId.Value,
            orchestrationInstance.Lifecycle.CreatedBy.Value.MapToDto(),
            activityInput.IdempotencyKey,
            data).ConfigureAwait(false);
    }

    public sealed record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        ForwardMeteredDataInputV1 ForwardMeteredDataInputV1,
        Guid IdempotencyKey);
}
