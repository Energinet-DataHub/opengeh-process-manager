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

using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;
using Energinet.DataHub.ProcessManager.Components.Abstractions.ValueObjects;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.Shared.V1.Model;

public sealed record EnqueueMeasureDataSyncV1(
    Actor Receiver,
    string MeteringPointId,
    MeteringPointType MeteringPointType,
    MeasurementUnit MeasureUnit,
    string ProductNumber,
    DateTimeOffset RegistrationDateTime,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    Resolution Resolution,
    IReadOnlyCollection<MeasureData> MeasureData,
    string GridAreaCode)
        : IEnqueueDataSyncDto
{
    public const string RouteName = "v1/enqueue_brs021";

    public string Route { get; } = RouteName;
}

public record MeasureData(
    int Position,
    decimal EnergyQuantity,
    Quality QuantityQuality);
