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

using Energinet.DataHub.ProcessManager.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Api.Model;

/// <summary>
/// Query for orchestration instances by name and other primary information.
/// Must be JSON serializable.
/// </summary>
public sealed record SearchOrchestrationInstancesByNameQuery
    : OrchestrationInstanceRequest<UserIdentityDto>
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="name">A common name to identity the orchestration which the instances was created from.</param>
    /// <param name="version">A version identifying a specific implementation of the orchestration which the instances was created from.</param>
    /// <param name="lifecycleState">Lifecycle states that orchestration instances can be in.</param>
    /// <param name="terminationState">Termination states that orchestration instances can be in.</param>
    /// <param name="startedAtOrLater"></param>
    /// <param name="terminatedAtOrEarlier"></param>
    public SearchOrchestrationInstancesByNameQuery(
        UserIdentityDto operatingIdentity,
        string name,
        int? version,
        OrchestrationInstanceLifecycleStates? lifecycleState,
        OrchestrationInstanceTerminationStates? terminationState,
        DateTimeOffset? startedAtOrLater,
        DateTimeOffset? terminatedAtOrEarlier)
            : base(operatingIdentity)
    {
        Name = name;
        Version = version;
        LifecycleState = lifecycleState;
        TerminationState = terminationState;
        StartedAtOrLater = startedAtOrLater;
        TerminatedAtOrEarlier = terminatedAtOrEarlier;
    }

    /// <summary>
    /// A common name to identity the orchestration description which the orchestration instances
    /// was created from.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A version identifying a specific implementation of the orchestration description which
    /// the orchestration instances was created from.
    /// </summary>
    public int? Version { get; }

    /// <summary>
    /// Lifecycle states that orchestration instances can be in.
    /// </summary>
    public OrchestrationInstanceLifecycleStates? LifecycleState { get; }

    /// <summary>
    /// Termination states that orchestration instances can be in.
    /// </summary>
    public OrchestrationInstanceTerminationStates? TerminationState { get; }

    /// <summary>
    /// The time (or later) when the orchestration instances was transitioned to the Running state.
    /// </summary>
    public DateTimeOffset? StartedAtOrLater { get; }

    /// <summary>
    /// The time (or earlier) when the orchestration instances was transitioned to the Terminated state.
    /// </summary>
    public DateTimeOffset? TerminatedAtOrEarlier { get; }
}
