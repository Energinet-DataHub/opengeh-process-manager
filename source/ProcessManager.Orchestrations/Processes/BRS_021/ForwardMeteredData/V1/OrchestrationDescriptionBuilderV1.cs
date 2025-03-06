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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Application.Registration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1;

internal class OrchestrationDescriptionBuilderV1 : IOrchestrationDescriptionBuilder
{
    public const int ValidationStep = 1;
    public const int ForwardToMeasurementStep = 2;
    public const int FindReceiverStep = 3;
    public const int EnqueueActorMessagesStep = 4;
    public static readonly OrchestrationDescriptionUniqueNameDto UniqueName = new("Brs_021_ForwardMeteredData", 1);

    public OrchestrationDescription Build()
    {
        var orchestrationDescriptionUniqueName = Brs_021_ForwardedMeteredData.V1;

        var description = new OrchestrationDescription(
            uniqueName: new OrchestrationDescriptionUniqueName(
                orchestrationDescriptionUniqueName.Name,
                orchestrationDescriptionUniqueName.Version),
            canBeScheduled: false,
            functionName: string.Empty);

        description.ParameterDefinition.SetFromType<ForwardMeteredDataInputV1>();
        description.AppendStepDescription("Forretningsvalidering");
        description.AppendStepDescription("Gemmer måledata");
        description.AppendStepDescription("Finder modtagere");
        description.AppendStepDescription("Udsender beskeder");

        return description;
    }
}
