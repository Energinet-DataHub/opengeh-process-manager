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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;

/// <summary>
/// Query for searching for Calculations orchestration instances.
/// Must be JSON serializable.
/// </summary>
public sealed record CalculationsQueryV1
    : SearchOrchestrationInstancesByCustomQuery<ICalculationsQueryResultV1>
{
    public const string RouteName = "v1/calculations";

    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    public CalculationsQueryV1(
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
    /// If this is <see langword="null"/> then all calculation types might be included in the search.
    /// However, if any query parameter for specific types has been specified
    /// (e.g. <see cref="IsInternalCalculation"/> then only those types are included in the search.
    /// </summary>
    public IReadOnlyCollection<CalculationTypeQueryParameterV1>? CalculationTypes { get; set; }

    /// <summary>
    /// If this is true, then only the following calculation types will be searched for:
    ///  - BRS-023/027
    /// </summary>
    public bool? IsInternalCalculation { get; set; }

    /// <summary>
    /// If this is specified, then only the following calculation types will be searched for:
    ///  - BRS-023/027
    /// </summary>
    public IReadOnlyCollection<string>? GridAreaCodes { get; set; }

    /// <summary>
    /// If this is specified, then only the following calculation types will be searched for:
    ///  - BRS-023/027
    ///  - BRS-021 Capacity Settlement
    /// </summary>
    public DateTimeOffset? PeriodStartDate { get; set; }

    /// <summary>
    /// If this is specified, then only the following calculation types will be searched for:
    ///  - BRS-023/027
    ///  - BRS-021 Capacity Settlement
    /// </summary>
    public DateTimeOffset? PeriodEndDate { get; set; }
}
