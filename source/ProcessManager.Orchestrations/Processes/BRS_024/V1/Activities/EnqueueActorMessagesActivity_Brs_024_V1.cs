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
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Activities;

public class EnqueueActorMessagesActivity_Brs_024_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_024_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestYearlyMeasurementsInputV1>();

        await EnqueueActorMessagesAsync(
            orchestrationInstance.Lifecycle.CreatedBy.Value,
            input,
            orchestrationInstanceInput).ConfigureAwait(false);
    }

    private Task EnqueueActorMessagesAsync(
        OperatingIdentity orchestrationCreatedBy,
        ActivityInput input,
        RequestYearlyMeasurementsInputV1 requestYearlyInput)
    {
        // Update this when we are getting data from the other subsystems
        var data = new RequestYearlyMeasurementsAcceptedV1(
            OriginalActorMessageId: requestYearlyInput.ActorMessageId,
            OriginalTransactionId: requestYearlyInput.TransactionId,
            MeteringPointId: requestYearlyInput.MeteringPointId,
            MeteringPointType: MeteringPointType.Consumption,
            ProductNumber: "123",
            RegistrationDateTime: DateTimeOffset.Parse("2050-01-01T12:00:00.0000000+01:00"),
            StartDateTime: DateTimeOffset.Parse("2050-01-01T12:00:00.0000000+01:00"),
            EndDateTime: DateTimeOffset.Parse("2050-01-02T12:00:00.0000000+01:00"),
            ActorNumber: ActorNumber.Create(requestYearlyInput.ActorNumber),
            ActorRole: ActorRole.FromName(requestYearlyInput.ActorRole),
            Resolution: Resolution.QuarterHourly,
            MeasureUnit: MeasurementUnit.Kilowatt,
            Measurements: new List<AcceptedMeteredData>(),
            GridAreaCode: "804");

        return _enqueueActorMessagesClient.EnqueueAsync(
            orchestration: Orchestration_Brs_024_V1.UniqueName,
            orchestrationInstanceId: input.InstanceId.Value,
            orchestrationStartedBy: orchestrationCreatedBy.MapToDto(),
            idempotencyKey: input.IdempotencyKey,
            data: data);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        Guid IdempotencyKey);
}
