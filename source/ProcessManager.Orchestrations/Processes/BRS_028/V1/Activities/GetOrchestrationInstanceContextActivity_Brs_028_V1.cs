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
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_028.V1.Models;
using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_028.V1.Options;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_028.V1.Activities;

/// <summary>
/// Set the orchestration instance lifecycle to running
/// </summary>
internal class GetOrchestrationInstanceContextActivity_Brs_028_V1(
    IOptions<OrchestrationOptions_Brs_028_V1> options)
{
    private readonly OrchestrationOptions_Brs_028_V1 _options = options.Value;

    [Function(nameof(GetOrchestrationInstanceContextActivity_Brs_028_V1))]
    public Task<OrchestrationInstanceContext> Run(
        [ActivityTrigger] ActivityInput input)
    {
        return Task.FromResult(new OrchestrationInstanceContext(
            input.InstanceId,
            _options));
    }

    public record ActivityInput(
        OrchestrationInstanceId InstanceId);
}
