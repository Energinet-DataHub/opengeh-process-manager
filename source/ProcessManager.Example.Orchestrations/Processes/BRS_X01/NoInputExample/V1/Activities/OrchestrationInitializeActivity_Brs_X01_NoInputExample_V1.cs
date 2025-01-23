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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Model;
using Microsoft.Azure.Functions.Worker;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X01.NoInputExample.V1.Activities;

internal class OrchestrationInitializeActivity_Brs_X01_NoInputExample_V1(
    IOrchestrationInstanceProgressRepository repository)
{
    private readonly IOrchestrationInstanceProgressRepository _repository = repository;

    [Function(nameof(OrchestrationInitializeActivity_Brs_X01_NoInputExample_V1))]
    public Task<OrchestrationExecutionPlan> Run(
        [ActivityTrigger] ActivityInput input)
    {
        return Task.FromResult(new OrchestrationExecutionPlan([]));
    }

    public record ActivityInput(
        OrchestrationInstanceId OrchestrationInstanceId);
}
