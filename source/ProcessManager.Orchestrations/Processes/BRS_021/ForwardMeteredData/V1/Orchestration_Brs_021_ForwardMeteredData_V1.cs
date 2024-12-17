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
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.DurableTask;
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
    internal const int EnqueueMessagesStep = 4;

    [Function(nameof(Orchestration_Brs_021_ForwardMeteredData_V1))]
    public async Task<string> Run(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetOrchestrationParameterValue<MeteredDataForMeasurementPointMessageInputV1>();

        if (input == null)
            return "Error: No input specified.";

        var defaultRetryOptions = CreateDefaultRetryOptions();

        await Task.CompletedTask;

        // Initialize
        await context.CallActivityAsync(
            nameof(OrchestrationInitializeActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);

        // Fetch Metering Point Master Data
        var meteringPointMasterData = await context.CallActivityAsync<MeteringPointMasterData?>(
            nameof(FetchMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1),
            new FetchMeteringPointMasterDataActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(
                new MeteringPointIdentification("1234567890")),
            defaultRetryOptions);

        if (meteringPointMasterData is null)
        {
            // We should poke EDI gently and tell them that we would like the actor to receive an RSM-09 message
            return "Success";
        }

        // Step: Validating
        await context.CallActivityAsync(
            nameof(PerformAsyncValidationActivity_Brs_021_ForwardMeteredData_V1),
            new PerformAsyncValidationActivity_Brs_021_ForwardMeteredData_V1.ActivityInput(Guid.Parse(context.InstanceId)),
            defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(ValidationStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);

        // Step: Storing
        await context.CallActivityAsync(
            nameof(StoreMeteredDataForMeasurementPointActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);
        //await context.WaitForExternalEvent<string>("Measurements_Notification");
        await context.CallActivityAsync(
            nameof(StoringStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);

        // Step: Find Receiver
        await context.CallActivityAsync(
            nameof(FindReceiversActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);
        await context.CallActivityAsync(
            nameof(FindReceiversTerminateActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);

        // Step: Enqueueing
        await context.CallActivityAsync(
            nameof(EnqueueMessagesActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);
        //await context.WaitForExternalEvent<string>("EDI_Notification");
        await context.CallActivityAsync(
            nameof(EnqueueMessagesStepTerminateActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);

        // Terminate
        await context.CallActivityAsync(
            nameof(OrchestrationTerminateActivity_Brs_021_ForwardMeteredData_V1),
            context.InstanceId,
            defaultRetryOptions);

        return "Success";
    }

    private static TaskOptions CreateDefaultRetryOptions()
    {
        return TaskOptions.FromRetryPolicy(new RetryPolicy(
            maxNumberOfAttempts: 5,
            firstRetryInterval: TimeSpan.FromSeconds(30),
            backoffCoefficient: 2.0));
    }
}
