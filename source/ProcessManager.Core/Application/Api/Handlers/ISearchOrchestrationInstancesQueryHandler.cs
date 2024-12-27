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

namespace Energinet.DataHub.ProcessManagement.Core.Application.Api.Handlers;

/// <summary>
/// Interface for handling a query for searching orchestration instances.
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResultItem">The result type of each item returned in the result list from the query. Must be a JSON serializable type.</typeparam>
public interface ISearchOrchestrationInstancesQueryHandler<TQuery, TResultItem>
    where TQuery : SearchOrchestrationInstancesByCustomQuery<TResultItem>
    where TResultItem : class
{
    /// <summary>
    /// Handles a query for searching orchestration instances.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <returns>Returns a result item for each matching orchestration instance.</returns>
    Task<IReadOnlyCollection<TResultItem>> HandleAsync(TQuery query);
}
