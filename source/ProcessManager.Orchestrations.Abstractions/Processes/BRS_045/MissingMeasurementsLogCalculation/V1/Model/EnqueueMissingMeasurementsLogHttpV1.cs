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
using Energinet.DataHub.ProcessManager.Components.Abstractions.EnqueueActorMessages;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_045.MissingMeasurementsLogCalculation.V1.Model;

/// <summary>
/// Used to enqueue actor messages for BRS-045 (Missing Measurements Log Calculation). The model is shared
/// with EDI, which uses the <see cref="EnqueueMissingMeasurementsLogHttpV1.RouteName"/> as the HTTP URI.
/// <remarks>
/// A message will be sent for each item in the <paramref name="Data"/> list.
/// </remarks>
/// </summary>
/// <param name="OrchestrationInstanceId"></param>
/// <param name="Data">The list of dates and metering point id's that are missing data on the given dates.</param>
public record EnqueueMissingMeasurementsLogHttpV1(
    Guid OrchestrationInstanceId,
    IReadOnlyCollection<EnqueueMissingMeasurementsLogHttpV1.DateWithMeteringPointIds> Data)
    : IEnqueueDataSyncDto
{
    public const string RouteName = "v1/enqueue_brs045";

    public string Route { get; } = RouteName;

    /// <summary>
    /// A representation of the message that will be sent to the given <paramref name="GridAccessProvider"/>.
    /// </summary>
    /// <param name="GridAccessProvider">The current grid access provider for the metering point, who should receive the message.</param>
    /// <param name="GridArea">The grid area for the metering point and grid access provider, used determine whether the actor has delegations.</param>
    /// <param name="Date">The date where measurements are missing for the given <paramref name="MeteringPointId"/>.</param>
    /// <param name="MeteringPointId">The metering point that is missing measurements on the given <paramref name="Date"/>.</param>
    public record DateWithMeteringPointIds(
        ActorNumber GridAccessProvider,
        string GridArea,
        DateTimeOffset Date,
        string MeteringPointId);
}
