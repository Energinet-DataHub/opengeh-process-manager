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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Core.Infrastructure.Database;
using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.CustomQueries.Examples.V1.Model;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.CustomQueries.Examples.V1;

internal class SearchExampleByIdHandlerV1(
    ProcessManagerReaderContext readerContext) :
        ISearchOrchestrationInstanceQueryHandler<ExampleByIdQueryV1, IExamplesQueryResultV1>
{
    private readonly ProcessManagerReaderContext _readerContext = readerContext;

    /// <summary>
    /// Get orchestration instance by id; or <see langword="null"/> if it doesn't exist.
    /// Also, if the orchestration instance found is not one of the types supported
    /// by <see cref="IExamplesQueryResultV1"/>, then the response will be <see langword="null"/>.
    /// </summary>
    public async Task<IExamplesQueryResultV1?> HandleAsync(ExampleByIdQueryV1 query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *

        var item =
            await SearchAsync(
                ExamplesQueryResultMapperV1.SupportedOrchestrationDescriptionNames,
                new OrchestrationInstanceId(query.Id))
            .ConfigureAwait(false);

        return item == default
            ? null
            : ExamplesQueryResultMapperV1.MapToDto(item.UniqueName, item.Instance);
    }

    /// <summary>
    /// Get orchestration instance by id, filtered by orchestration description names.
    /// </summary>
    /// <param name="orchestrationDescriptionNames"></param>
    /// <param name="orchestrationInstanceId"></param>
    /// <returns>
    /// Use the returned unique name to determine which orchestration description
    /// the orchestration instance was created from.
    /// </returns>
    private async Task<(OrchestrationDescriptionUniqueName UniqueName, OrchestrationInstance Instance)> SearchAsync(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        OrchestrationInstanceId orchestrationInstanceId)
    {
        var queryable = _readerContext
            .OrchestrationDescriptions
                .Where(x => orchestrationDescriptionNames.Contains(x.UniqueName.Name))
            .Join(
                _readerContext.OrchestrationInstances,
                description => description.Id,
                instance => instance.OrchestrationDescriptionId,
                (description, instance) => new { description.UniqueName, instance })
            .Where(x => x.instance.Id == orchestrationInstanceId)
            .Select(x => ValueTuple.Create(x.UniqueName, x.instance));

        return await queryable.SingleOrDefaultAsync().ConfigureAwait(false);
    }
}
