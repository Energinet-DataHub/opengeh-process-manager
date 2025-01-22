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
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

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

        if (input == null)
            return "Error: No input specified.";

        // Initialize
        var instanceId = await InitializeOrchestrationAsync(context);

        // Fetch Metering Point Master Data
        var meteringPointMasterData =
            await context
                .CallActivityAsync<GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1.ActivityOutput>(
            nameof(GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1),
            new GetMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                input.MeteringPointId,
                input.StartDateTime,
                input.EndDateTime),
            _defaultRetryOptions);

        // Step: Validating
        var errors = await PerformValidationAsync(
            context,
            instanceId,
            meteringPointMasterData.MeteringPointMasterData);

        // If there are errors, we stop the orchestration and inform EDI to pass along the errors
        if (errors.Count != 0)
        {
            return await HandleAsynchronousValidationErrors(context, instanceId, input.TransactionId, errors);
        }

        // Step: Storing
        await context.CallActivityAsync(
            nameof(StoreMeteredDataForMeteringPointActivity_Brs_021_ForwardMeteredData_V1),
            new StoreMeteredDataForMeteringPointActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                input),
            _defaultRetryOptions);
        //await context.WaitForExternalEvent<string>("Measurements_Notification");
        await context.CallActivityAsync(
            nameof(StoringStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            new StoringStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(instanceId),
            _defaultRetryOptions);

        // Step: Find Receiver
        await context.CallActivityAsync(
            nameof(TransitionStepToRunningActivity_Brs_021_ForwardMeteredData_V1),
            new TransitionStepToRunningActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                FindReceiverStep),
            _defaultRetryOptions);

        // Find Receivers
        var findReceiversActivityOutput =
        await context.CallActivityAsync<FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityOutput>(
            nameof(FindReceiversActivity_Brs_021_ForwardMeteredData_V1),
            new FindReceiversActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                input.MeteringPointType!,
                input.StartDateTime,
                input.EndDateTime!,
                meteringPointMasterData.MeteringPointMasterData.First()),
            _defaultRetryOptions);

        // Terminate Step: Find Receiver
        await context.CallActivityAsync(
            nameof(TransitionStepToTerminatedActivity_Brs_021_ForwardMeteredData_V1),
            new TransitionStepToTerminatedActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                FindReceiverStep,
                OrchestrationStepTerminationState.Succeeded),
            _defaultRetryOptions);

        // Step: Enqueueing // TODO: Skip if no receivers found
        var idempotencyKey = context.NewGuid();
        await context.CallActivityAsync(
            nameof(EnqueueActorMessagesActivity_Brs_021_ForwardMeteredData_V1),
            new EnqueueActorMessagesActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                input,
                idempotencyKey,
                findReceiversActivityOutput.MarketActorRecipients),
            _defaultRetryOptions);
        //await context.WaitForExternalEvent<string>("EDI_Notification");
        await context.CallActivityAsync(
            nameof(EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            new EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
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

    private async Task<string> HandleAsynchronousValidationErrors(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        string inputTransactionId,
        IReadOnlyCollection<string> errors)
    {
        var rejectMessage =
            await context.CallActivityAsync<CreateRejectMessageActivity_Brs_021_ForwardMeteredData_V1.ActivityOutput>(
                nameof(CreateRejectMessageActivity_Brs_021_ForwardMeteredData_V1),
                new CreateRejectMessageActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                    instanceId,
                    inputTransactionId,
                    errors),
                _defaultRetryOptions);

        var idempotencyKey = context.NewGuid();

        await context.CallActivityAsync(
            nameof(EnqueueRejectMessageActivity_Brs_021_V1),
            new EnqueueRejectMessageActivity_Brs_021_V1.ActivityInput(
                instanceId,
                rejectMessage.RejectMessage,
                idempotencyKey),
            _defaultRetryOptions);

        var messagesEnqueuedSuccessfully = await WaitForEnqueueActorMessagesResponseFromEdiAsync(context, instanceId);

        if (!messagesEnqueuedSuccessfully)
        {
            await context.CallActivityAsync(
                nameof(EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
                new EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                    instanceId,
                    OrchestrationStepTerminationState.Failed),
                _defaultRetryOptions);

            return "Error: Message was not enqueued.";
        }

        await context.CallActivityAsync(
            nameof(EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            new EnqueueActorMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                instanceId,
                OrchestrationStepTerminationState.Succeeded),
            _defaultRetryOptions);

        return "Success";
    }

    private async Task<bool> WaitForEnqueueActorMessagesResponseFromEdiAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId)
    {
        // TODO: Use monitor pattern to wait for "notify" from EDI
        var waitForMessagesEnqueued = context.CreateTimer(TimeSpan.FromSeconds(1), CancellationToken.None);
        await waitForMessagesEnqueued;

        return true;
    }

    private async Task<IReadOnlyCollection<string>> PerformValidationAsync(
        TaskOrchestrationContext context,
        OrchestrationInstanceId instanceId,
        IReadOnlyCollection<Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Model.MeteringPointMasterData> meteringPointMasterData)
    {
        var errors = await context.CallActivityAsync<IReadOnlyCollection<string>>(
            nameof(PerformValidationActivity_Brs_021_ForwardMeteredData_V1),
            new PerformValidationActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
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
