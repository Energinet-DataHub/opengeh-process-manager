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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ElectricalHeatingCalculation.V1.Orchestration.Steps;

internal class EnqueueActorMessagesStep(
    TaskOrchestrationContext context,
    TaskRetryOptions defaultRetryOptions,
    OrchestrationInstanceContext orchestrationInstanceContext)
    : StepExecutor(
        context,
        defaultRetryOptions,
        orchestrationInstanceContext.OrchestrationInstanceId)
{
    internal const string StepDescription = "Besked dannelse";
    internal const int EnqueueActorMessagesStepSequence = 3;

    protected override int StepSequenceNumber => EnqueueActorMessagesStepSequence;

    protected override Task<StepInstanceTerminationState> OnExecuteAsync()
    {
        // TODO - Alex: Implement and call activities to enqueue messages
        // 1. step
        // build statement
        //      - build SQL query
        // loop over results using steaming
        // make/map rsm-12 message

        // 2. Get actor info for the message

        // 3. step enqueue message:
        // await EnqueueAcceptedActorMessagesAsync(
        //        orchestrationInstance,
        //        forwardMeteredDataInput,
        //        receiversWithMeteredData)
        //    .ConfigureAwait(false);
        // Blocking: Kvitteringer from many messages?

        return Task.FromResult(StepInstanceTerminationState.Succeeded);
    }
}
