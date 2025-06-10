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
using Energinet.DataHub.ProcessManager.Components.MeteringPointMasterData.Extensions;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_025.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using Quality = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Quality;
using Resolution = Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects.Resolution;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_025.V1.Activities;

public class EnqueueActorMessagesActivity_Brs_025_V1(
    IOrchestrationInstanceProgressRepository progressRepository,
    IEnqueueActorMessagesClient enqueueActorMessagesClient,
    IMeasurementsClient measurementsClient,
    IClock clock)
{
    private readonly IOrchestrationInstanceProgressRepository _progressRepository = progressRepository;
    private readonly IEnqueueActorMessagesClient _enqueueActorMessagesClient = enqueueActorMessagesClient;
    private readonly IMeasurementsClient _measurementsClient = measurementsClient;
    private readonly IClock _clock = clock;

    [Function(nameof(EnqueueActorMessagesActivity_Brs_025_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _progressRepository
            .GetAsync(input.InstanceId)
            .ConfigureAwait(false);

        var orchestrationInstanceInput = orchestrationInstance.ParameterValue.AsType<RequestMeasurementsInputV1>();

        var message = await GetMessagesAsync(orchestrationInstanceInput).ConfigureAwait(false);

        await EnqueueActorMessagesAsync(
            orchestrationInstance.Lifecycle.CreatedBy.Value,
            input,
            message).ConfigureAwait(false);
    }

    // TODO: Move mappers + tests
    private static TimeSpan GetResolutionDuration(Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution resolution)
    {
        switch (resolution)
        {
            case Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.QuarterHourly:
                return TimeSpan.FromMinutes(15);
            case Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Hourly:
                return TimeSpan.FromHours(1);
            case Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Daily:
                return TimeSpan.FromDays(1);
            case Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution.Monthly:
                throw new InvalidOperationException("Monthly resolution to duration is not supported, since a month is not a fixed duration.");
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown resolution.");
        }
    }

    private static MeasurementUnit MapMeasureUnit(Unit unit)
    {
        return unit switch
        {
            Unit.kW => MeasurementUnit.Kilowatt,
            Unit.kWh => MeasurementUnit.KilowattHour,
            Unit.kVArh => MeasurementUnit.KiloVoltAmpereReactiveHour,
            Unit.MW => MeasurementUnit.Megawatt,
            Unit.MWh => MeasurementUnit.MegawattHour,
            Unit.MVAr => MeasurementUnit.MegaVoltAmpereReactivePower,
            Unit.Tonne => MeasurementUnit.MetricTon,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), $"Unknown unit: {unit}"),
        };
    }

    private static Quality MapQuality(Energinet.DataHub.Measurements.Abstractions.Api.Models.Quality quality)
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

    private static Resolution MapResolution(Energinet.DataHub.Measurements.Abstractions.Api.Models.Resolution quality)
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

    // TODO: create tests for this method
    private async Task<RequestMeasurementsAcceptedV1> GetMessagesAsync(RequestMeasurementsInputV1 orchestrationInstanceInput)
    {
        var measurementPointDtos =
            await GetMeasurementPointsFromMeasurements(orchestrationInstanceInput).ConfigureAwait(false);

        var measurements = CreateMeasurements(measurementPointDtos);

        return GenerateMessage(
            orchestrationInstanceInput,
            measurements);
    }

    private IReadOnlyCollection<Measurement> CreateMeasurements(IReadOnlyCollection<MeasurementPointDto> measurementPointDtos)
    {
        var measurements = new List<Measurement>();
        var currentMeasurementPoints = new List<MeasurementPoint>();
        MeasurementPointDto? previousPoint = null;
        DateTimeOffset? currentMeasurementStartedAt = null;

        foreach (var measurementPointDto in measurementPointDtos.OrderBy(x => x.RegistrationTime))
        {
            if (previousPoint != null)
            {
                var expectedTime = previousPoint.RegistrationTime + GetResolutionDuration(previousPoint.Resolution);
                if (measurementPointDto.RegistrationTime != expectedTime)
                {
                    // Create a new Measurement with the current group of points
                    measurements.Add(new Measurement(
                        Resolution: MapResolution(previousPoint.Resolution),
                        MeasureUnit: MapMeasureUnit(previousPoint.Unit),
                        StartDateTime: currentMeasurementStartedAt!.Value,
                        EndDateTime: measurementPointDto.RegistrationTime, // The end time is the start time of the next point
                        MeasurementPoints: currentMeasurementPoints));

                    // Start a new group of points
                    currentMeasurementPoints = new List<MeasurementPoint>();
                    currentMeasurementStartedAt = measurementPointDto.RegistrationTime;
                }
            }

            // Add the current point to the current group of points
            currentMeasurementPoints.Add(new MeasurementPoint(
                Position: measurementPointDto.Order,
                EnergyQuantity: measurementPointDto.Quantity,
                QuantityQuality: MapQuality(measurementPointDto.Quality)));

            currentMeasurementStartedAt ??= measurementPointDto.RegistrationTime;

            previousPoint = measurementPointDto;
        }

        // Add the last group as a Measurement
        if (currentMeasurementPoints.Any())
        {
            measurements.Add(new Measurement(
                Resolution: MapResolution(previousPoint!.Resolution),
                MeasureUnit: MapMeasureUnit(previousPoint.Unit),
                StartDateTime: currentMeasurementStartedAt!.Value,
                EndDateTime: previousPoint.RegistrationTime + GetResolutionDuration(previousPoint.Resolution),
                MeasurementPoints: currentMeasurementPoints));
        }

        return measurements;
    }

    private async Task<IReadOnlyCollection<MeasurementPointDto>> GetMeasurementPointsFromMeasurements(
        RequestMeasurementsInputV1 orchestrationInstanceInput)
    {
        var to = InstantPatternWithOptionalSeconds.Parse(orchestrationInstanceInput.EndDateTime!).Value;
        var from = InstantPatternWithOptionalSeconds.Parse(orchestrationInstanceInput.StartDateTime!).Value;
        var measurementsQuery = new GetByPeriodQuery(
            MeteringPointId: orchestrationInstanceInput.MeteringPointId,
            To: to,
            From: from);

        var measurementPointsFromMeasurements = await _measurementsClient
            .GetCurrentByPeriodAsync(measurementsQuery).ConfigureAwait(false);
        return measurementPointsFromMeasurements.AsReadOnly();
    }

    private RequestMeasurementsAcceptedV1 GenerateMessage(
        RequestMeasurementsInputV1 input,
        IReadOnlyCollection<Measurement> measurements)
    {
        // TODO: GetMasterdata and update product number and grid area code
        return new RequestMeasurementsAcceptedV1(
            OriginalActorMessageId: input.ActorMessageId,
            OriginalTransactionId: input.TransactionId,
            MeteringPointId: input.MeteringPointId,
            MeteringPointType: MeteringPointType.Consumption,   // Elmark data
            ProductNumber: "123",                               // Elmark data?
            ActorNumber: ActorNumber.Create(input.ActorNumber),
            ActorRole: ActorRole.FromName(input.ActorRole),
            MeasureUnit: MeasurementUnit.Kilowatt,              // Elmark data
            Measurements: measurements,
            GridAreaCode: "804");
    }

    private Task EnqueueActorMessagesAsync(
        OperatingIdentity orchestrationCreatedBy,
        ActivityInput input,
        RequestMeasurementsAcceptedV1 message)
    {
        return _enqueueActorMessagesClient.EnqueueAsync(
            orchestration: Orchestration_Brs_025_V1.UniqueName,
            orchestrationInstanceId: input.InstanceId.Value,
            orchestrationStartedBy: orchestrationCreatedBy.MapToDto(),
            idempotencyKey: input.IdempotencyKey,
            data: message);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        Guid IdempotencyKey);
}
