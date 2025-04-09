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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;

public record InputExampleResultV1 :
    OrchestrationInstanceTypedDto<InputV1>,
    IExamplesQueryResultV1
{
    public InputExampleResultV1(
        Guid id,
        OrchestrationInstanceLifecycleDto lifecycle,
        IReadOnlyCollection<StepInstanceDto> steps,
        string customState,
        InputV1 parameterValue)
            : base(
                id,
                lifecycle,
                steps,
                customState,
                parameterValue)
    {
    }
}
