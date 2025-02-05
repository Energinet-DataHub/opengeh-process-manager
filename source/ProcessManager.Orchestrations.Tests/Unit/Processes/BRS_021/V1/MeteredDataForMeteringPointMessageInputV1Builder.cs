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

using Energinet.DataHub.ProcessManager.Components.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_021.V1;

public class MeteredDataForMeteringPointMessageInputV1Builder
{
    public static MeteredDataForMeteringPointMessageInputV1 Build() =>
        new(
            MessageId: "MessageId",
            AuthenticatedActorId: Guid.NewGuid(),
            ActorNumber: "1111111111111",
            ActorRole: ActorRole.GridAccessProvider.Name,
            TransactionId: "TransactionId",
            MeteringPointId: "MeteringPointId",
            MeteringPointType: MeteringPointType.Production.Code,
            ProductNumber: "ProductNumber",
            MeasureUnit: MeasurementUnit.KilowattHour.Code,
            RegistrationDateTime: "2025-01-01T00:00Z",
            Resolution: Resolution.QuarterHourly.Code,
            StartDateTime: "2025-01-01T00:00Z",
            EndDateTime: "2025-01-01T00:15Z",
            GridAccessProviderNumber: "GridAccessProviderNumber",
            DelegatedGridAreaCodes: null,
            EnergyObservations:
            [
                new(
                    "1",
                    "1024",
                    Quality.AsProvided.Code),
            ]);
}
