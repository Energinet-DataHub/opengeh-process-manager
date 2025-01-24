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

using Energinet.DataHub.ElectricityMarket.Integration;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DurableTask;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;
using Energinet.DataHub.ProcessManager.Shared.Processes.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using MeteringPointMasterData = Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointMasterData;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

internal class Orchestration_Brs_021_ForwardMeteredData_V1
{
    internal const int ValidatingStep = 1;
    internal const int StoringMeteredDataStep = 2;
    internal const int FindReceiverStep = 3;
    internal const int EnqueueActorMessagesStep = 4;

    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = Brs_021_ForwardedMeteredData.V1;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_021_ForwardMeteredData_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_021_ForwardMeteredData_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<MeteredDataForMeteringPointMessageInputV1>();
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        if (input == null)
            return "Error: No input specified.";

        // Initialize
        await context.CallActivityAsync(
            nameof(OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1),
            new OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(instanceId),
            _defaultRetryOptions);

        // Start Step: Find Receiver
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_V1),
            new TransitionStepToRunningActivity_V1.ActivityInput(
                instanceId,
                FindReceiverStep),
            _defaultRetryOptions);

        // Terminate Step: Find Receiver
        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_V1),
            new TransitionStepToTerminatedActivity_V1.ActivityInput(
                instanceId,
                FindReceiverStep,
                OrchestrationStepTerminationState.Succeeded),
            _defaultRetryOptions);

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_021_ForwardMeteredData_V1),
            new OrchestrationTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(instanceId),
            _defaultRetryOptions);

        return "Success";
    }

    private static TaskOptions CreateDefaultRetryOptions() =>
        TaskOptions.FromRetryPolicy(
            new RetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(30),
                backoffCoefficient: 2.0));
}
