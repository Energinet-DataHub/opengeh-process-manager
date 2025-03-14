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
using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.CustomQueries;

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
    /// <param name="createdByActorNumber">Optional actor number of the actor that created. If not provided, the filter won't be applied.</param>
    /// <param name="createdByActorRole">Optional actor role of the actor to filter by. If not provided, the filter won't be applied.</param>
    public ActorRequestQuery(
        UserIdentityDto operatingIdentity,
        DateTimeOffset activatedAtOrLater,
        DateTimeOffset activatedAtOrEarlier,
        ActorNumber? createdByActorNumber,
        ActorRole? createdByActorRole)
            : base(operatingIdentity)
    {
        OrchestrationDescriptionNames = [
            Brs_026.Name,
            Brs_028.Name];
        ActivatedAtOrLater = activatedAtOrLater;
        ActivatedAtOrEarlier = activatedAtOrEarlier;
        CreatedByActorNumber = createdByActorNumber;
        CreatedByActorRole = createdByActorRole;
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

    /// <summary>
    /// Optional actor number of the actor to filter by. If not provided, the filter won't be applied.
    /// </summary>
    public ActorNumber? CreatedByActorNumber { get; }

    /// <summary>
    /// Optional actor role of the actor to filter by. If not provided, the filter won't be applied.
    /// </summary>
    public ActorRole? CreatedByActorRole { get; }
}
