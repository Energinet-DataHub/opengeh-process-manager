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
/// Base class for implementing custom queries for orchestration instances.
/// Must be JSON serializable.
/// </summary>
/// <typeparam name="TItem">The result type of each item returned in the result list. Must be a JSON serializable type.</typeparam>
public abstract record SearchOrchestrationInstancesByCustomQuery<TItem>
    : OrchestrationInstanceRequest<UserIdentityDto>
        where TItem : class
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    /// <param name="name">A common name to identity the orchestration which the instances was created from.</param>
    public SearchOrchestrationInstancesByCustomQuery(
        UserIdentityDto operatingIdentity,
        string name)
            : base(operatingIdentity)
    {
        Name = name;
    }

    /// <summary>
    /// A common name to identity the orchestration description which the orchestration instances
    /// was created from.
    /// </summary>
    public string Name { get; }
}
