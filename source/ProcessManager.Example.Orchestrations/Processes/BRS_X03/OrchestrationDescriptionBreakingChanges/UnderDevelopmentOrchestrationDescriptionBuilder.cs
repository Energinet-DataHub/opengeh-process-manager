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

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Processes.BRS_X03.OrchestrationDescriptionBreakingChanges;

/// <summary>
/// An orchestration description used to test breaking changes when synchronizing the orchestration register.
/// This is used in the Given_OrchestrationDescriptionBreakingChanges_When_CallingHealthCheck_Then_IsUnhealthy
/// health check test.
/// </summary>
internal class UnderDevelopmentOrchestrationDescriptionBuilder : IOrchestrationDescriptionBuilder
{
    public static readonly OrchestrationDescriptionUniqueName UniqueName = new(
        name: "UnderDevelopmentTestDescription",
        version: 1);

    public OrchestrationDescription Build()
    {
        var description = new OrchestrationDescription(
            uniqueName: UniqueName,
            canBeScheduled: false,
            functionName: "UnderDevelopmentTestDescriptionFunctionNameV1");

        description.IsUnderDevelopment = true;

        description.AppendStepDescription("Step 1");

        return description;
    }
}
