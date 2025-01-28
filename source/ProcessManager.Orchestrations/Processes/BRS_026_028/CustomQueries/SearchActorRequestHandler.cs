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

using Energinet.DataHub.ProcessManager.Core.Application.Api.Handlers;
using Energinet.DataHub.ProcessManager.Core.Application.Orchestration;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_026.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.BRS_028.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_026_028.CustomQueries;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_026_028.CustomQueries;

internal class SearchActorRequestHandler(
    IOrchestrationInstanceQueries queries) :
        ISearchOrchestrationInstancesQueryHandler<ActorRequestQuery, IActorRequestQueryResult>
{
    private readonly IOrchestrationInstanceQueries _queries = queries;

    public async Task<IReadOnlyCollection<IActorRequestQueryResult>> HandleAsync(ActorRequestQuery query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *

        // DateTimeOffset values must be in "round-trip" ("o"/"O") format to be parsed correctly
        // See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier
        var activatedAtOrLater = Instant.FromDateTimeOffset(query.ActivatedAtOrLater);
        var activatedAtOrEarlier = Instant.FromDateTimeOffset(query.ActivatedAtOrEarlier);

        var results = await _queries
            .SearchAsync(
                query.OrchestrationDescriptionNames,
                activatedAtOrLater,
                activatedAtOrEarlier)
            .ConfigureAwait(false);

        return results
            .Select(item => MapToConcreteResultDto(item.UniqueName, item.Instance))
            .ToList();
    }

    private IActorRequestQueryResult MapToConcreteResultDto(OrchestrationDescriptionUniqueName uniqueName, OrchestrationInstance instance)
    {
        if (uniqueName.Name == Brs_026.Name)
        {
            var original = instance.MapToTypedDto<RequestCalculatedEnergyTimeSeriesInputV1>();
            return new RequestCalculatedEnergyTimeSeriesResult(
                original.Id,
                original.Lifecycle,
                original.Steps,
                original.CustomState,
                original.ParameterValue);
        }

        if (uniqueName.Name == Brs_028.Name)
        {
            var original = instance.MapToTypedDto<RequestCalculatedWholesaleServicesInputV1>();
            return new RequestCalculatedWholesaleServicesResult(
                original.Id,
                original.Lifecycle,
                original.Steps,
                original.CustomState,
                original.ParameterValue);
        }

        throw new InvalidOperationException($"Unsupported unique name '{uniqueName.Name}'.");
    }
}
