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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_024.V1.Orchestration;

public class OrchestrationDescriptionBuilder : IOrchestrationDescriptionBuilder
{
    public OrchestrationDescription Build()
    {
        var orchestrationDescriptionUniqueName = Orchestration_Brs_024_V1.UniqueName;

        var description = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(
                orchestrationDescriptionUniqueName.Name,
                orchestrationDescriptionUniqueName.Version),
            canBeScheduled: true,
            functionName: nameof(Orchestration_Brs_024_V1));

        return description;
    }
}
