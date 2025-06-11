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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Unit.Processes.BRS_024.V1;

public class RequestYearlyMeasurementsInputV1Builder
{
    private const string ActorMessageId = "MessageId";
    private const string TransactionId = "TransactionId";
    private const string MeteringPointId = "MeteringPointId";
    private const string ReceivedAt = "2024-12-31T23:00Z";

    private readonly ActorRole _actorRole = ActorRole.GridAccessProvider;
    private readonly ActorNumber _actorNumber = ActorNumber.Create("1234567890123");

    public RequestYearlyMeasurementsInputV1 Build()
    {
        return new RequestYearlyMeasurementsInputV1(
            ActorMessageId: ActorMessageId,
            TransactionId: TransactionId,
            ActorNumber: _actorNumber,
            ActorRole: _actorRole,
            ReceivedAt: ReceivedAt,
            MeteringPointId: MeteringPointId);
    }
}
