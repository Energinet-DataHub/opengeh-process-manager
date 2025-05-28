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

using Energinet.DataHub.Measurements.Abstractions.Api.Models;
using Energinet.DataHub.Measurements.Abstractions.Api.Queries;
using Energinet.DataHub.Measurements.Client;
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Activities;

public class EnqueueActorMessagesActivity_Brs_024_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    IMeasurementsClient measurementsClient,
    IClock clock)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly IMeasurementsClient _measurementsClient = measurementsClient;
    private readonly IClock _clock = clock;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_024_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestYearlyMeasurementsInputV1>();

        var measurements = await GetMeasurementsAsync(orchestrationInstanceInput).ConfigureAwait(false);

        await EnqueueActorMessagesAsync(
            orchestrationInstance.Lifecycle.CreatedBy.Value,
            input,
            measurements,
            orchestrationInstanceInput).ConfigureAwait(false);
    }

    private async Task<IEnumerable<AcceptedMeteredData>> GetMeasurementsAsync(RequestYearlyMeasurementsInputV1 orchestrationInstanceInput)
    {
        // TODO: Correct this, when we get the periods from elmark.
        var now = _clock.GetCurrentInstant();
        var measurementsQuery = new GetAggregateByPeriodQuery(
            MeteringPointIds: new List<string>() { orchestrationInstanceInput.MeteringPointId },
            To: now,
            From: now.Minus(Duration.FromDays(365)),
            Aggregation: Aggregation.Quarter);

        var measurements = (await _measurementsClient.GetAggregatedByPeriodAsync(measurementsQuery)
            .ConfigureAwait(false))
            .ToList();

        if (measurements.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one measurement for metering point {orchestrationInstanceInput.MeteringPointId}, but found {measurements.Count}.");
        }

        var measurement = measurements.Single();

        var keys = measurement.PointAggregationGroups.Keys.ToList();

        var data = new List<AcceptedMeteredData>();
        return data;
    }

    private Task EnqueueActorMessagesAsync(
        OperatingIdentity orchestrationCreatedBy,
        ActivityInput input,
        IEnumerable<AcceptedMeteredData> measurements,
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
            Measurements: measurements.ToList(),
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
