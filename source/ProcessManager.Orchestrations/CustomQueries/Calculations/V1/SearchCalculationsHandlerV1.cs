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
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.CustomQueries.Calculations.V1.Model;
using Energinet.DataHub.ProcessManager.Shared.Api.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Energinet.DataHub.ProcessManager.Orchestrations.CustomQueries.Calculations.V1;

internal class SearchCalculationsHandlerV1(
    ProcessManagerReaderContext readerContext) :
        ISearchOrchestrationInstancesQueryHandler<CalculationsQueryV1, ICalculationsQueryResultV1>
{
    private readonly ProcessManagerReaderContext _readerContext = readerContext;

    public async Task<IReadOnlyCollection<ICalculationsQueryResultV1>> HandleAsync(CalculationsQueryV1 query)
    {
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
        //
        // NOTICE:
        // The query also carries information about the user executing the query,
        // so if necessary we can validate their data access.
        //
        // * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *

        var results = await SearchAsync(query).ConfigureAwait(false);

        return results
            .Select(item => CalculationsQueryResultMapperV1.MapToDto(item.UniqueName, item.Instance))
            .ToList();
    }

    /// <summary>
    /// Get all orchestration instances filtered by the query.
    /// </summary>
    /// <returns>Use the returned unique name to determine which orchestration description
    /// a given orchestration instance was created from.</returns>
    private async Task<IReadOnlyCollection<(
            OrchestrationDescriptionUniqueName UniqueName,
            OrchestrationInstance Instance)>>
        SearchAsync(
            CalculationsQueryV1 query)
    {
        var orchestrationDescriptionNames = query.GetOrchestrationDescriptionNames();
        var uniqueNamesById = await GetUniqueNamesByIdDictionaryAsync(orchestrationDescriptionNames).ConfigureAwait(false);

        var sql = BuildSql(orchestrationDescriptionNames, query);
        var queryable = _readerContext.OrchestrationInstances
            .FromSql(sql)
            .Select(instance => ValueTuple.Create(uniqueNamesById[instance.OrchestrationDescriptionId], instance));

#if DEBUG
        var queryStringForDebugging = queryable.ToQueryString();
#endif

        return await queryable.ToListAsync().ConfigureAwait(false);
    }

    private FormattableString BuildSql(
        IReadOnlyCollection<string> orchestrationDescriptionNames,
        CalculationsQueryV1 query)
    {
        var lifecycleStates = query.LifecycleStates.MapToDomain();
        var terminationState = query.TerminationState.MapToDomain();

        var scheduledAtOrLater = query.ScheduledAtOrLater.ToNullableInstant();
        var startedAtOrLater = query.StartedAtOrLater.ToNullableInstant();
        var terminatedAtOrEarlier = query.TerminatedAtOrEarlier.ToNullableInstant();

        var wholesaleCalculationTypes = query
            .CalculationTypes?
                .Where(x => Enum.IsDefined(typeof(Abstractions.Processes.BRS_023_027.V1.Model.CalculationType), (int)x))
                .ToList();

        return $"""
            SELECT
                [oi].[Id],
                [oi].[ActorMessageId],
                [oi].[IdempotencyKey],
                [oi].[MeteringPointId],
                [oi].[OrchestrationDescriptionId],
                [oi].[RowVersion],
                [oi].[TransactionId],
                [oi].[CustomState] as CustomState_SerializedValue,
                [oi].[ParameterValue] as ParameterValue_SerializedValue,
                [oi].[Lifecycle_CreatedAt],
                [oi].[Lifecycle_QueuedAt],
                [oi].[Lifecycle_ScheduledToRunAt],
                [oi].[Lifecycle_StartedAt],
                [oi].[Lifecycle_State],
                [oi].[Lifecycle_TerminatedAt],
                [oi].[Lifecycle_TerminationState],
                [oi].[Lifecycle_CanceledBy_ActorNumber],
                [oi].[Lifecycle_CanceledBy_ActorRole],
                [oi].[Lifecycle_CanceledBy_IdentityType],
                [oi].[Lifecycle_CanceledBy_UserId],
                [oi].[Lifecycle_CreatedBy_ActorNumber],
                [oi].[Lifecycle_CreatedBy_ActorRole],
                [oi].[Lifecycle_CreatedBy_IdentityType],
                [oi].[Lifecycle_CreatedBy_UserId]
            FROM
                [pm].[OrchestrationDescription] AS [od]
            INNER JOIN
                [pm].[OrchestrationInstance] AS [oi] ON [od].[Id] = [oi].[OrchestrationDescriptionId]
            WHERE
                [od].[Name] IN (
                    SELECT [names].[value]
                    FROM OPENJSON({orchestrationDescriptionNames}) WITH ([value] nvarchar(max) '$') AS [names]
                )
                AND (
                    {lifecycleStates} is null
                    OR [oi].[Lifecycle_State] IN (
                        SELECT [lifecyclestates].[value]
                        FROM OPENJSON({lifecycleStates}) WITH ([value] int '$') AS [lifecyclestates])
                )
                AND (
                    {terminationState} is null
                    OR [oi].[Lifecycle_TerminationState] = {terminationState}
                )
                AND (
                    {startedAtOrLater} is null
                    OR {startedAtOrLater} <= [oi].[Lifecycle_StartedAt]
                )
                AND (
                    {terminatedAtOrEarlier} is null
                    OR [oi].[Lifecycle_TerminatedAt] <= {terminatedAtOrEarlier}
                )
                AND (
                    {scheduledAtOrLater} is null
                    OR {scheduledAtOrLater} <= [oi].[Lifecycle_ScheduledToRunAt]
                )
                AND (
                    [oi].[OrchestrationDescriptionId] NOT IN (SELECT Id FROM [pm].OrchestrationDescription WHERE Name = 'Brs_023_027')
                    OR (
                        [oi].[OrchestrationDescriptionId] IN (SELECT Id FROM [pm].OrchestrationDescription WHERE Name = 'Brs_023_027')
                        AND (
                            {query.IsInternalCalculation} is null
                            OR (
                                CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.IsInternalCalculation') AS bit) = {query.IsInternalCalculation}
                            )
                        )
                        AND (
                            {wholesaleCalculationTypes} is null
                            OR (
                                CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.CalculationType') AS int) IN (
                                    SELECT [calculationtypes].[value]
                                    FROM OPENJSON({wholesaleCalculationTypes}) WITH ([value] int '$') AS [calculationtypes]
                                )
                            )
                        )
                        AND (
                            {query.GridAreaCodes} is null
                            OR (
                                EXISTS (
            						SELECT
            							value
            						FROM
            							OPENJSON(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null), '$.GridAreaCodes')
            						WHERE
            							value IN (
            								SELECT
            									[gridareacodes].[value]
            								FROM OPENJSON({query.GridAreaCodes}) WITH ([value] int '$') AS [gridareacodes]
            							)
                                )
                            )
                        )
                        -- *******************************************************************************************************************************************************
                        -- This period check follows the algorithm "bool overlap = a.start < b.end && b.start < a.end"
                        -- where a = query and b = calculationInput.
                        -- See https://stackoverflow.com/questions/13513932/algorithm-to-detect-overlapping-periods for more info.
                        AND (
                            {query.PeriodStartDate} is null
                            OR (
                                {query.PeriodStartDate} < CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.PeriodEndDate') AS datetime2(7))
                            )
                        )
                        AND (
                            {query.PeriodEndDate} is null
                            OR (
                                CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.PeriodStartDate') AS datetime2(7)) < {query.PeriodEndDate}
                            )
                        )
                        -- *******************************************************************************************************************************************************
                    )
                )
                AND (
                    [oi].[OrchestrationDescriptionId] NOT IN (SELECT Id FROM [pm].OrchestrationDescription WHERE Name = 'Brs_021_CapacitySettlementCalculation')
                    OR (
                        [oi].[OrchestrationDescriptionId] IN (SELECT Id FROM [pm].OrchestrationDescription WHERE Name = 'Brs_021_CapacitySettlementCalculation')
                        AND (
                            {query.PeriodStartDate} is null
                            OR (
                                YEAR({query.PeriodStartDate}) < CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.Year') AS int)
                            )
                            OR (
                                YEAR({query.PeriodStartDate}) = CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.Year') AS int)
                                AND MONTH({query.PeriodStartDate}) <= CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.Month') AS int)
                            )
                        )
                        AND (
                            {query.PeriodEndDate} is null
                            OR (
                                CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.Year') AS int) < YEAR({query.PeriodEndDate})
                            )
                            OR (
                                CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.Year') AS int) = YEAR({query.PeriodEndDate})
                                AND CAST(JSON_VALUE(IIF(ISJSON([oi].[ParameterValue]) = 1, [oi].[ParameterValue], null),'$.Month') AS int) <= MONTH({query.PeriodEndDate})
                            )
                        )
                    )
                )
            """;
    }

    /// <summary>
    /// Build a dictionary of relevant orchestration description unique names.
    /// We can use this to combine orchestration instances with their
    /// orchestration description unique name counterpart.
    /// </summary>
    private async Task<IReadOnlyDictionary<OrchestrationDescriptionId, OrchestrationDescriptionUniqueName>>
        GetUniqueNamesByIdDictionaryAsync(
            IReadOnlyCollection<string> orchestrationDescriptionNames)
    {
        var orchestrationDescriptions = await _readerContext
            .OrchestrationDescriptions
                .Where(x => orchestrationDescriptionNames.Contains(x.UniqueName.Name))
            .ToListAsync()
            .ConfigureAwait(false);

        return orchestrationDescriptions
            .Select(x => new KeyValuePair<OrchestrationDescriptionId, OrchestrationDescriptionUniqueName>(x.Id, x.UniqueName))
            .ToDictionary();
    }
}
