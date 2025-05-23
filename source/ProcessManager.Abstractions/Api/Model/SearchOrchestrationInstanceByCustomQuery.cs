﻿// Copyright 2020 Energinet DataHub A/S
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
/// Base class for implementing custom queries for searching a single orchestration instance.
/// Must be JSON serializable.
/// </summary>
/// <remarks>
/// Using JSON polymorphism on <typeparamref name="TResultItem"/> makes it possible to
/// specify a base type, and be able to deserialize it into multiple concrete types.
/// </remarks>
/// <typeparam name="TResultItem">
/// The type (or base type) of the item returned.
/// Must be a JSON serializable type.
/// </typeparam>
public abstract record SearchOrchestrationInstanceByCustomQuery<TResultItem>
    : OrchestrationInstanceRequest<UserIdentityDto>
        where TResultItem : class
{
    /// <summary>
    /// Construct query.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the query.</param>
    public SearchOrchestrationInstanceByCustomQuery(
        UserIdentityDto operatingIdentity)
            : base(operatingIdentity)
    {
    }

    /// <summary>
    /// A query name used to route the underlying request to the correct custom query trigger.
    /// </summary>
    public abstract string QueryRouteName { get; }
}
