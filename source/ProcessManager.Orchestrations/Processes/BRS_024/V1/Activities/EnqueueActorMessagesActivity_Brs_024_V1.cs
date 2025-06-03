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

using Azure.Messaging.ServiceBus;
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
using Aggregation = Energinet.DataHub.Measurements.Abstractions.Api.Models.Aggregation;
using Quality = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Quality;
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

        var message = await GetMessagesAsync(orchestrationInstanceInput).ConfigureAwait(false);

        await EnqueueActorMessagesAsync(
            orchestrationInstance.Lifecycle.CreatedBy.Value,
            input,
            message).ConfigureAwait(false);
    }

    private async Task<RequestYearlyMeasurementsAcceptedV1> GetMessagesAsync(RequestYearlyMeasurementsInputV1 orchestrationInstanceInput)
    {
        // TODO: Correct this, when we get the periods from elmark.
        var now = _clock.GetCurrentInstant();
        var measurementsQuery = new GetAggregateByPeriodQuery(
            MeteringPointIds: new List<string>() { orchestrationInstanceInput.MeteringPointId },
            To: now,
            From: now.Minus(Duration.FromDays(365)),
            Aggregation: Aggregation.Year);

        var measurements = (await _measurementsClient.GetAggregatedByPeriodAsync(measurementsQuery)
            .ConfigureAwait(false))
            .ToList();

        if (measurements.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one measurement for metering point {orchestrationInstanceInput.MeteringPointId}, but found {measurements.Count}.");
        }

        var measurement = measurements.Single();

        // The keys are a combination of metering point id, date and resolution.
        // I have no idea how we can utilize this. Hence I will ignore the values of the keys.
        var keys = measurement.PointAggregationGroups.Keys.ToList();

        var aggregatedMeasurements = keys.Select(
            key =>
                CreateAggregatedMeasurement(measurement.PointAggregationGroups[key])).ToList();

        return GenerateMessage(
            orchestrationInstanceInput,
            aggregatedMeasurements);
    }

    private RequestYearlyMeasurementsAcceptedV1 GenerateMessage(
        RequestYearlyMeasurementsInputV1 input,
        IReadOnlyCollection<AggregatedMeasurement> aggregatedMeasurements)
    {
        return new RequestYearlyMeasurementsAcceptedV1(
            OriginalActorMessageId: input.ActorMessageId,
            OriginalTransactionId: input.TransactionId,
            MeteringPointId: input.MeteringPointId,
            MeteringPointType: MeteringPointType.Consumption,   // Elmark data
            ProductNumber: "123",                               // Elmark data?
            ActorNumber: ActorNumber.Create(input.ActorNumber),
            ActorRole: ActorRole.FromName(input.ActorRole),
            MeasureUnit: MeasurementUnit.Kilowatt,              // Elmark data
            AggregatedMeasurements: aggregatedMeasurements,
            GridAreaCode: "804");
    }

    private AggregatedMeasurement CreateAggregatedMeasurement(PointAggregationGroup grouping)
    {
        // Single() is valid due to the data structure of the PointAggregationGroup.
        // If the year changes, then we will have a new PointAggregationGroup.
        // Since the yaar is part of the key in the PointAggregationGroups dictionary,
        var point = grouping.PointAggregations.Single();
        return new AggregatedMeasurement(
            StartDateTime: grouping.From.ToDateTimeOffset(),
            EndDateTime: grouping.To.ToDateTimeOffset(),
            Resolution: MapResolution(grouping.Resolution),
            EnergyQuantity: point.Quantity,
            QuantityQuality: MapQuality(point.Quality));
    }

    private Quality MapQuality(Energinet.DataHub.Measurements.Abstractions.Api.Models.Quality quality)
    {
        return quality switch
        {
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Quality.Missing => Quality.NotAvailable,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Quality.Estimated => Quality.Estimated,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Quality.Calculated => Quality.Calculated,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Quality.Measured => Quality.AsProvided,
            _ => throw new ArgumentOutOfRangeException(nameof(quality), $"Unknown quality: {quality}"),
        };
    }

    private Resolution MapResolution(Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution quality)
    {
        return quality switch
        {
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.QuarterHourly => Resolution.QuarterHourly,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Hourly => Resolution.Hourly,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Daily => Resolution.Daily,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Monthly => Resolution.Monthly,
            Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Yearly => Resolution.Yearly,
            _ => throw new ArgumentOutOfRangeException(nameof(quality), $"Unknown quality: {quality}"),
        };
    }

    private Task EnqueueActorMessagesAsync(
        OperatingIdentity orchestrationCreatedBy,
        ActivityInput input,
        RequestYearlyMeasurementsAcceptedV1 message)
    {
        return _enqueueActorMessagesClient.EnqueueAsync(
            orchestration: Orchestration_Brs_024_V1.UniqueName,
            orchestrationInstanceId: input.InstanceId.Value,
            orchestrationStartedBy: orchestrationCreatedBy.MapToDto(),
            idempotencyKey: input.IdempotencyKey,
            data: message);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        Guid IdempotencyKey);
}
