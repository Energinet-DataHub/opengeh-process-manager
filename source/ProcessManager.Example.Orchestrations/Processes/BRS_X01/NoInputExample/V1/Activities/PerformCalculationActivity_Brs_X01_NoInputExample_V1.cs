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

using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Activities;

internal class PerformCalculationActivity_Brs_X01_NoInputExample_V1(
    IOrchestrationInstanceProgressRepository repository)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;

    [Function(nameof(PerformCalculationActivity_Brs_X01_NoInputExample_V1))]
    public async Task<ActivityOutput> Run(
        [ActivityTrigger] ActivityInput input)
    {
        var orchestrationInstance = await _repository.GetAsync(input.OrchestrationInstanceId).ConfigureAwait(false);

        // Do some work here instead of delaying ...
        await Task.Delay(100).ConfigureAwait(false);

        return new ActivityOutput(
            CalculationResult: 42);
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);

    public record ActivityOutput(
        int CalculationResult);
}
