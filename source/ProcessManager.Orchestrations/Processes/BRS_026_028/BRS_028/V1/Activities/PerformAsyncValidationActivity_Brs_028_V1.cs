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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_028.V1.Model;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.BRS_028.V1.Activities;

/// <summary>
/// Perform async validation (and set step to running)
/// </summary>
internal class PerformAsyncValidationActivity_Brs_028_V1
{
    [Function(nameof(PerformAsyncValidationActivity_Brs_028_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var isValid = await PerformAsyncValidationAsync(input.RequestInput).ConfigureAwait(false);

        return isValid;
    }

    private async Task<ActivityOutput> PerformAsyncValidationAsync(RequestCalculatedWholesaleServicesInputV1 requestInput)
    {
        // TODO: Perform async validation instead of delay
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        return new ActivityOutput(
            IsValid: true,
            ValidationError: null);
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId,
        RequestCalculatedWholesaleServicesInputV1 RequestInput);

    public record ActivityOutput(
        bool IsValid,
        RequestCalculatedWholesaleServicesRejectedV1? ValidationError);
}
