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
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;

/// <summary>
/// Query for searching for Examples orchestration instances.
/// Must be JSON serializable.
/// </summary>
public record ExamplesQueryV1
    : SearchOrchestrationInstancesByCustomQuery<IExamplesQueryResultV1>
{
    public const string RouteName = "v1/examples";

    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    public ExamplesQueryV1(
        UserIdentityDto operatingIdentity)
            : base(operatingIdentity)
    {
    }

    /// <inheritdoc/>
    public override string QueryRouteName => RouteName;

    public IReadOnlyCollection<OrchestrationInstanceLifecycleState>? LifecycleStates { get; set; }

    public OrchestrationInstanceTerminationState? TerminationState { get; set; }

    public DateTimeOffset? ScheduledAtOrLater { get; set; }

    public DateTimeOffset? StartedAtOrLater { get; set; }

    public DateTimeOffset? TerminatedAtOrEarlier { get; set; }

    /// <summary>
    /// If this is <see langword="null"/> then all example types might be included in the search.
    /// However, if any query parameter for specific types has been specified
    /// (e.g. <see cref="SkippedStepTwo"/> then only those types are included in the search.
    /// </summary>
    public IReadOnlyCollection<ExampleTypeQueryParameterV1>? ExampleTypes { get; set; }

    /// <summary>
    /// If this is true, then only the following calculation types will be searched for:
    ///  - BRS-X01 Input example
    ///
    /// Search criteria to check if step two was skipped.
    /// </summary>
    public bool? SkippedStepTwo { get; set; }
}
