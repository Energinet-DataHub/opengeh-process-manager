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

using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample.V1.Steps;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X02.NotifyOrchestrationInstanceExample;

internal class OrchestrationDescriptionBuilder : IOrchestrationDescriptionBuilder
{
    public OrchestrationDescription Build()
    {
        var orchestrationDescriptionUniqueName = Brs_X02_NotifyOrchestrationInstanceExample.V1;

        var description = new OrchestrationDescription(
            uniqueName: orchestrationDescriptionUniqueName.MapToDomain(),
            canBeScheduled: true,
            functionName: nameof(Orchestration_Brs_X02_NotifyOrchestrationInstanceExample_V1));

        description.ParameterDefinition.SetFromType<NotifyOrchestrationInstanceExampleInputV1>();

        description.AppendStepDescription(WaitForNotifyEventStep.StepDescription);

        return description;
    }
}
