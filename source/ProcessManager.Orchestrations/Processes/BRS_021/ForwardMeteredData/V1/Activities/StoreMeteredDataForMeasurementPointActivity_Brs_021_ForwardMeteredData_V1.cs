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

using System.Globalization;
using Energinet.DataHub.ProcessManager.Components.Datahub.ValueObjects;
using Energinet.DataHub.ProcessManager.Components.Measurements;
using Energinet.DataHub.ProcessManager.Components.Measurements.Model;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;
using Microsoft.Azure.Functions.Worker;
using NodaTime;
using NodaTime.Text;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;

internal class StoreMeteredDataForMeasurementPointActivity_Brs_021_ForwardMeteredData_V1(
    IClock clock,
    IOrchestrationInstanceProgressRepository progressRepository,
    IMeasurementsMeteredDataClient measurementsMeteredDataClient)
    : ProgressActivityBase(
        clock,
        progressRepository)
{
    private readonly IMeasurementsMeteredDataClient _measurementsMeteredDataClient = measurementsMeteredDataClient;

    [Function(nameof(StoreMeteredDataForMeasurementPointActivity_Brs_021_ForwardMeteredData_V1))]
    public async Task Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await ProgressRepository
            .GetAsync(input.OrchestrationInstanceId)
            .ConfigureAwait(false);

        await TransitionStepToRunningAsync(
                Orchestration_Brs_021_ForwardMeteredData_V1.StoringMeteredDataStep,
                orchestrationInstance)
            .ConfigureAwait(false);

        var points = input.MeteredDataForMeasurementPointMessageInput.EnergyObservations
            .Select(x => new Point(
                ParsePosition(x.Position),
                ParseQuantity(x.EnergyQuantity),
                ParseQuality(x.QuantityQuality)))
            .ToList();

        var meteredData = new MeteredDataForMeasurementPoint(
            input.OrchestrationInstanceId.ToString(),
            input.MeteredDataForMeasurementPointMessageInput.MeteringPointId!,
            input.MeteredDataForMeasurementPointMessageInput.TransactionId,
            ParseDateTime(input.MeteredDataForMeasurementPointMessageInput.RegistrationDateTime),
            ParseDateTime(input.MeteredDataForMeasurementPointMessageInput.StartDateTime),
            ParseDateTime(input.MeteredDataForMeasurementPointMessageInput.EndDateTime),
            ParseMeteringPointType(input.MeteredDataForMeasurementPointMessageInput.MeteringPointType),
            input.MeteredDataForMeasurementPointMessageInput.ProductNumber!,
            ParseMeasureUnit(input.MeteredDataForMeasurementPointMessageInput.MeasureUnit),
            ParseResolution(input.MeteredDataForMeasurementPointMessageInput.Resolution),
            points);

        await _measurementsMeteredDataClient.SendAsync(meteredData, CancellationToken.None).ConfigureAwait(false);
    }

    private static Instant ParseDateTime(string? input)
    {
        return InstantPatternWithOptionalSeconds.Parse(input!).Value;
    }

    private Resolution ParseResolution(string? resolution)
    {
        if (string.IsNullOrEmpty(resolution))
        {
            throw new ArgumentException("Resolution cannot be null or empty", nameof(resolution));
        }

        return Resolution.FromCode(resolution);
    }

    private MeasurementUnit ParseMeasureUnit(string? measureUnit)
    {
        if (string.IsNullOrEmpty(measureUnit))
        {
            throw new ArgumentException("Metering point type cannot be null or empty", nameof(measureUnit));
        }

        return MeasurementUnit.FromCode(measureUnit);
    }

    private MeteringPointType ParseMeteringPointType(string? meteringPointType)
    {
        if (string.IsNullOrEmpty(meteringPointType))
        {
            throw new ArgumentException("Metering point type cannot be null or empty", nameof(meteringPointType));
        }

        return MeteringPointType.FromCode(meteringPointType);
    }

    private Quality ParseQuality(string? quality)
    {
        if (string.IsNullOrEmpty(quality))
        {
            throw new ArgumentException("Quality cannot be null or empty", nameof(quality));
        }

        return Quality.FromCode(quality);
    }

    private decimal ParseQuantity(string? sourceQuantity)
    {
        if (string.IsNullOrEmpty(sourceQuantity))
        {
            throw new ArgumentException("Quantity cannot be null or empty", nameof(sourceQuantity));
        }

        if (!decimal.TryParse(sourceQuantity, out var quantity))
        {
            throw new FormatException($"Invalid quantity format: {sourceQuantity}");
        }

        return quantity;
    }

    private int ParsePosition(string? sourcePosition)
    {
        if (string.IsNullOrEmpty(sourcePosition))
        {
            throw new ArgumentException("Position cannot be null or empty", nameof(sourcePosition));
        }

        if (!int.TryParse(sourcePosition, out var position))
        {
            throw new FormatException($"Invalid position format: {sourcePosition}");
        }

        return position;
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId,
        MeteredDataForMeasurementPointMessageInputV1 MeteredDataForMeasurementPointMessageInput);
}
