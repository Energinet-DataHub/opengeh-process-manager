﻿// Copyright 2020 Energinet DataHub A/S
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

using Energinet.DataHub.ProcessManager.Abstractions.Contracts;
using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_024.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.Extensions.Logging;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1;

public class RequestYearlyMeasurementsHandlerV1(
    ILogger<RequestYearlyMeasurementsHandlerV1> logger,
    IStartOrchestrationInstanceMessageCommands commands)
    : StartOrchestrationInstanceHandlerBase<RequestYearlyMeasurementsInputV1>(logger)
{
    private readonly IStartOrchestrationInstanceMessageCommands _commands = commands;

    public override bool CanHandle(StartOrchestrationInstanceV1 startOrchestrationInstance) =>
        startOrchestrationInstance.OrchestrationName == Brs_024.V1.Name &&
        startOrchestrationInstance.OrchestrationVersion == Brs_024.V1.Version;

    protected override async Task StartOrchestrationInstanceAsync(
        ActorIdentity actorIdentity,
        RequestYearlyMeasurementsInputV1 input,
        string idempotencyKey,
        string actorMessageId,
        string transactionId,
        string? meteringPointId)
    {
        await _commands.StartNewOrchestrationInstanceAsync(
                actorIdentity,
                Orchestration_Brs_024_V1.UniqueName.MapToDomain(),
                input,
                skipStepsBySequence: [],
                new IdempotencyKey(idempotencyKey),
                new ActorMessageId(actorMessageId),
                new TransactionId(transactionId),
                meteringPointId is not null ? new MeteringPointId(meteringPointId) : null)
            .ConfigureAwait(false);
    }
}
