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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.ActorRequestProcessExample.V1.Model;

/// <summary>
/// Start a BRS-X02 ActorRequestProcessExample actor request process example
/// </summary>
public record StartActorRequestProcessExampleCommandV1
    : StartOrchestrationInstanceMessageCommand<ActorRequestProcessExampleInputV1>
{
    public StartActorRequestProcessExampleCommandV1(
        ActorIdentityDto operatingIdentity,
        ActorRequestProcessExampleInputV1 inputParameter,
        string idempotencyKey,
        string actorMessageId,
        string transactionId)
        : base(operatingIdentity, Brs_X02_ActorRequestProcessExample.V1, inputParameter, idempotencyKey, actorMessageId, transactionId, null)
    {
    }
}
