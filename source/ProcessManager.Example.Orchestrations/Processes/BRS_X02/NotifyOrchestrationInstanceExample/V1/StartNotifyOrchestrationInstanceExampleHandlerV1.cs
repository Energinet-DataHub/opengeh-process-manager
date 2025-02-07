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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Microsoft.Extensions.Logging;
using NodaTime.Extensions;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;

internal class StartNotifyOrchestrationInstanceExampleHandlerV1(
    ILogger<StartNotifyOrchestrationInstanceExampleHandlerV1> logger,
    IStartOrchestrationInstanceMessageCommands commands)
    : StartOrchestrationInstanceFromMessageHandlerBase<NotifyOrchestrationInstanceExampleInputV1>(logger)
{
    private readonly IStartOrchestrationInstanceMessageCommands _commands = commands;

    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        NotifyOrchestrationInstanceExampleInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        await _commands.StartNewOrchestrationInstanceAsync(
                actorIdentity,
                OrchestrationDescriptionUniqueName.FromDto(Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1.UniqueName),
                input,
                skipStepsBySequence: [],
                new IdempotencyKey(idempotencyKey),
                new ActorMessageId(actorMessageId),
                new TransactionId(transactionId),
                meteringPointId is not null ? new MeteringPointId(meteringPointId) : null)
            .ConfigureAwait(false);
    }
}
