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

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

/// <summary>
/// Query for orchestration instance by id.
/// Must be JSON serializable.
/// </summary>
public sealed record GetOrchestrationInstanceByIdQuery
    : OrchestrationInstanceRequest<UserIdentityDto>
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="id">Id of the orchestration instance.</param>
    public GetOrchestrationInstanceByIdQuery(
        UserIdentityDto operatingIdentity,
        Guid id)
            : base(operatingIdentity)
    {
        Id = id;
    }

    /// <summary>
    /// Id of the orchestration instance.
    /// </summary>
    public Guid Id { get; }
}
