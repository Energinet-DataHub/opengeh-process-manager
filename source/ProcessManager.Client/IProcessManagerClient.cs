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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.SendMeasurements;

namespace Energinet.DataHub.ProcessManager.Client;

/// <summary>
/// Client for using the Process Manager / Orchestrations API.
/// </summary>
public interface IProcessManagerClient
{
    /// <summary>
    /// Schedule an orchestration instance and return its id.
    /// </summary>
    Task<Guid> ScheduleNewOrchestrationInstanceAsync(
        ScheduleOrchestrationInstanceCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancel a scheduled orchestration instance.
    /// </summary>
    Task CancelScheduledOrchestrationInstanceAsync(
        CancelScheduledOrchestrationInstanceCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Start an orchestration instance, and return its id.
    /// </summary>
    Task<Guid> StartNewOrchestrationInstanceAsync(
        StartOrchestrationInstanceCommand<UserIdentityDto> command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Get orchestration instance by id.
    /// </summary>
    Task<OrchestrationInstanceTypedDto> GetOrchestrationInstanceByIdAsync(
        GetOrchestrationInstanceByIdQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Get orchestration instance by id, and cast original input parameter to <see cref="IInputParameterDto"/>.
    /// </summary>
    Task<OrchestrationInstanceTypedDto<TInputParameterDto>> GetOrchestrationInstanceByIdAsync<TInputParameterDto>(
        GetOrchestrationInstanceByIdQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto;

    /// <summary>
    /// Get orchestration instance by idempotency key, and cast orignal input parameter to <see cref="IInputParameterDto"/>.
    /// </summary>
    Task<OrchestrationInstanceTypedDto<TInputParameterDto>?> GetOrchestrationInstanceByIdempotencyKeyAsync<TInputParameterDto>(
        GetOrchestrationInstanceByIdempotencyKeyQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto;

    /// <summary>
    /// Get all orchestration instances filtered by their related orchestration definition name and version,
    /// and their lifecycle / termination states.
    /// Returns orchestration instances.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstanceTypedDto>> SearchOrchestrationInstancesByNameAsync(
        SearchOrchestrationInstancesByNameQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Get all orchestration instances filtered by their related orchestration definition name and version,
    /// and their lifecycle / termination states.
    /// Returns orchestration instances including their original input parameter value.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstanceTypedDto<TInputParameterDto>>> SearchOrchestrationInstancesByNameAsync<TInputParameterDto>(
        SearchOrchestrationInstancesByNameQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto;

    /// <summary>
    /// Get orchestration instance, or null, by a custom query.
    /// </summary>
    /// <remarks>
    /// Using JSON polymorphism on <typeparamref name="TItem"/> makes it possible to
    /// specify a base type, and be able to deserialize it into multiple concrete types.
    /// </remarks>
    /// <typeparam name="TItem">
    /// The type (or base type) of the item returned.
    /// Must be a JSON serializable type.
    /// </typeparam>
    Task<TItem?> SearchOrchestrationInstanceByCustomQueryAsync<TItem>(
        SearchOrchestrationInstanceByCustomQuery<TItem> query,
        CancellationToken cancellationToken)
            where TItem : class;

    /// <summary>
    /// Get all orchestration instances filtered by a custom query.
    /// </summary>
    /// <remarks>
    /// Using JSON polymorphism on <typeparamref name="TItem"/> makes it possible to
    /// specify a base type, and be able to deserialize it into multiple concrete types.
    /// </remarks>
    /// <typeparam name="TItem">
    /// The type (or base type) of each item returned in the list.
    /// Must be a JSON serializable type.
    /// </typeparam>
    Task<IReadOnlyCollection<TItem>> SearchOrchestrationInstancesByCustomQueryAsync<TItem>(
        SearchOrchestrationInstancesByCustomQuery<TItem> query,
        CancellationToken cancellationToken)
            where TItem : class;

    /// <summary>
    /// Get Send Measurements instance by idempotency key.
    /// </summary>
    Task<SendMeasurementsInstanceDto?> GetSendMeasurementsInstanceByIdempotencyKeyAsync(
        GetSendMeasurementsInstanceByIdempotencyKeyQuery query,
        CancellationToken cancellationToken);
}
