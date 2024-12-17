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

using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1;

/// <summary>
/// Query for searching for BRS-X01.
/// Must be JSON serializable.
/// </summary>
public record ExampleQuery
    : SearchOrchestrationInstancesByCustomQuery<ExampleQueryResult>
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="skippedStepTwo"> search criteria to check if step two was skipped</param>
    public ExampleQuery(
        UserIdentityDto operatingIdentity,
        bool skippedStepTwo = default)
            : base(
                operatingIdentity,
                new Brs_X01_Example_V1().Name)
    {
        SkippedStepTwo = skippedStepTwo;
    }

    public OrchestrationInstanceLifecycleStates? LifecycleState { get; set; }

    public OrchestrationInstanceTerminationStates? TerminationState { get; set; }

    public DateTimeOffset? StartedAtOrLater { get; set; }

    public DateTimeOffset? TerminatedAtOrEarlier { get; set; }

    public bool SkippedStepTwo { get; set; }
}
