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
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DurableTask;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Activities;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026.V1.Activities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

internal class Orchestration_Brs_021_ForwardMeteredData_V1
{
    internal const int ValidatingStep = 1;
    internal const int StoringMeteredDataStep = 2;
    internal const int FindReceiverStep = 3;
    internal const int EnqueueMessagesStep = 4;

    private readonly TaskOptions _defaultRetryOptions;

    public Orchestration_Brs_021_ForwardMeteredData_V1()
    {
        _defaultRetryOptions = CreateDefaultRetryOptions();
    }

    [Function(nameof(Orchestration_Brs_021_ForwardMeteredData_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<MeteredDataForMeasurementPointMessageInputV1>();

        if (input == null)
            return "Error: No input specified.";

        // Initialize
        var instanceId = await InitializeOrchestrationAsync(context);

        // Fetch Metering Point Master Data
        var meteringPointMasterData = await context.CallActivityAsync<IReadOnlyCollection<MeteringPointMasterData>>(
            nameof(FetchMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1),
            new FetchMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                input.MeteringPointId,
                input.StartDateTime,
                input.EndDateTime),
            _defaultRetryOptions);

        // Step: Validating
        var errors = await PerformAsynchronousValidationAsync(context, instanceId, meteringPointMasterData);

        // If there are errors, we stop the orchestration and inform EDI to pass along the errors
        if (errors.Count != 0)
        {
            await context.CallActivityAsync(
                nameof(EnqueueRejectMessageActivity_Brs_021_V1),
                new EnqueueRejectMessageActivity_Brs_021_V1.ActivityInput(
                    instanceId,
                    errors),
                _defaultRetryOptions);

            var wasMessageEnqueued = await WaitForEnqueueMessagesResponseFromEdiAsync(context, instanceId);

            if (!wasMessageEnqueued)
            {
                await context.CallActivityAsync(
                    nameof(EnqueueMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
                    new EnqueueMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                        instanceId,
                        OrchestrationStepTerminationStates.Failed),
                    _defaultRetryOptions);

                return "Error: Message was not enqueued.";
            }
            else
            {
                await context.CallActivityAsync(
                    nameof(EnqueueMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
                    new EnqueueMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                        instanceId,
                        OrchestrationStepTerminationStates.Succeeded),
                    _defaultRetryOptions);

                return "Success";
            }
        }

        // Step: Storing
        await context.CallActivityAsync(
            nameof(StoreMeteredDataForMeasurementPointActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);
        //await context.WaitForExternalEvent<string>("Measurements_Notification");
        await context.CallActivityAsync(
            nameof(StoringStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);

        // Step: Find Receiver
        await context.CallActivityAsync(
            nameof(FindReceiversActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(FindReceiversTerminateActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);

        // Step: Enqueueing
        await context.CallActivityAsync(
            nameof(EnqueueMessagesActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);
        //await context.WaitForExternalEvent<string>("EDI_Notification");
        await context.CallActivityAsync(
            nameof(EnqueueMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_021_ForwardMeteredData_V1),
            instanceId,
            _defaultRetryOptions);

        return "Success";
    }

    private static TaskOptions CreateDefaultRetryOptions() =>
        TaskOptions.FromRetryPolicy(
            new RetryPolicy(
                maxNumberOfAttempts: 5,
                firstRetryInterval: TimeSpan.FromSeconds(30),
                backoffCoefficient: 2.0));

    private async Task<bool> WaitForEnqueueMessagesResponseFromEdiAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId)
    {
        // TODO: Use monitor pattern to wait for "notify" from EDI
        var waitForMessagesEnqueued = context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
        await waitForMessagesEnqueued;

        return true;
    }

    private async Task<IReadOnlyCollection<string>> PerformAsynchronousValidationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        IReadOnlyCollection<MeteringPointMasterData> meteringPointMasterData)
    {
        var errors = await context.CallActivityAsync<IReadOnlyCollection<string>>(
            nameof(PerformAsyncValidationActivity_Brs_021_ForwardMeteredData_V1),
            new PerformAsyncValidationActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                meteringPointMasterData),
            _defaultRetryOptions);

        await context.CallActivityAsync(
            nameof(ValidationStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            new ValidationStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(instanceId),
            _defaultRetryOptions);

        return errors;
    }

    private async Task<OrchestrationInstanceId> InitializeOrchestrationAsync(TaskOrchestrationContext context)
    {
        var instanceId = new OrchestrationInstanceId(Guid.Parse(context.InstanceId));

        await context.CallActivityAsync(
            nameof(OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1),
            new OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(instanceId),
            _defaultRetryOptions);

        return instanceId;
    }
}
