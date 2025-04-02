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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;

/// <summary>
/// Can be used with EF Core method "SqlQuery" to retrieve Orchestration Instances
/// using raw SQL statements.
/// </summary>
public record OrchestrationInstanceCustomQueryRow(
        string OrchestrationDescription_Name,
        int OrchestrationDescription_Version,

        Guid Id,
        string ParameterValue,
        string CustomState,
        string? IdempotencyKey,
        string? ActorMessageId,
        string? TransactionId,
        string? MeteringPointId,

        Instant Lifecycle_CreatedAt,
        Instant? Lifecycle_QueuedAt,
        Instant? Lifecycle_ScheduledToRunAt,
        Instant? Lifecycle_StartedAt,
        OrchestrationInstanceLifecycleState Lifecycle_State,
        Instant? Lifecycle_TerminatedAt,
        OrchestrationInstanceTerminationState? Lifecycle_TerminationState,

        string Lifecycle_CreatedBy_IdentityType,
        string? Lifecycle_CreatedBy_ActorNumber,
        string? Lifecycle_CreatedBy_ActorRole,
        Guid? Lifecycle_CreatedBy_UserId,

        string? Lifecycle_CanceledBy_IdentityType,
        string? Lifecycle_CanceledBy_ActorNumber,
        string? Lifecycle_CanceledBy_ActorRole,
        Guid? Lifecycle_CanceledBy_UserId,

        string Step_Description,
        int Step_Sequence,
        string Step_CustomState,

        bool Step_Lifecycle_CanBeSkipped,
        Instant? Step_Lifecycle_StartedAt,
        StepInstanceLifecycleState Step_Lifecycle_State,
        Instant? Step_Lifecycle_TerminatedAt,
        StepInstanceTerminationState? Step_Lifecycle_TerminationState);
