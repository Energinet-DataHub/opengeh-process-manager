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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_028.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026;

// TODO:
// Should be moved to another namespace because this is shared between BRS 026 + 028.
// We have talked about combining these BRS's into ne top-folder similar to BRS 023 + 027,
// and then use subfolders to split them per orchestration OR perhaps even use the same orchestration
// because their logic is very similar.

/// <summary>
/// Query for searching for BRS-026 or BRS-028 orchestration instances.
/// Must be JSON serializable.
/// </summary>
public sealed record ActorRequestQuery
    : SearchOrchestrationInstancesByCustomQuery<IActorRequestQueryResult>
{
    public const string RouteName = "brs_026_028";

    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="activatedAtOrLater">The time (or later) when the orchestration instances was queued or scheduled to run at.</param>
    /// <param name="activatedAtOrEarlier">The time (or earlier) when the orchestration instances was queued or scheduled to run at.</param>
    public ActorRequestQuery(
        UserIdentityDto operatingIdentity,
        DateTimeOffset activatedAtOrLater,
        DateTimeOffset activatedAtOrEarlier)
            : base(operatingIdentity)
    {
        OrchestrationDescriptionNames = [
            Brs_026.Name,
            new Brs_028_V1().Name];
        ActivatedAtOrLater = activatedAtOrLater;
        ActivatedAtOrEarlier = activatedAtOrEarlier;
    }

    /// <inheritdoc/>
    public override string QueryRouteName => RouteName;

    /// <summary>
    /// The names of the orchestration descriptions to filter by.
    /// </summary>
    public IReadOnlyCollection<string> OrchestrationDescriptionNames { get; }

    /// <summary>
    /// The time (or later) when the orchestration instances was queued or scheduled to run at.
    /// </summary>
    public DateTimeOffset ActivatedAtOrLater { get; }

    /// <summary>
    /// The time (or earlier) when the orchestration instances was queued or scheduled to run at.
    /// </summary>
    public DateTimeOffset ActivatedAtOrEarlier { get; }
}
