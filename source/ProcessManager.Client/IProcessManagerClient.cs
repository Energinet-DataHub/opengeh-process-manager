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

using Energinet.DataHub.ProcessManager.Api.Model;
using Energinet.DataHub.ProcessManager.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Client;

/// <summary>
/// Client for using the Process Manager / Orchestrations API.
/// </summary>
public interface IProcessManagerClient
{
    /// <summary>
    /// Schedule an orchestration instance and return its id.
    /// </summary>
    Task<Guid> ScheduleNewOrchestrationInstanceAsync<TInputParameterDto>(
        ScheduleOrchestrationInstanceCommand<TInputParameterDto> command,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto;

    /// <summary>
    /// Cancel a scheduled orchestration instance.
    /// </summary>
    Task CancelScheduledOrchestrationInstanceAsync(
        CancelScheduledOrchestrationInstanceCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Start an orchestration instance and return its id.
    /// </summary>
    Task<Guid> StartNewOrchestrationInstanceAsync<TInputParameterDto>(
        StartOrchestrationInstanceCommand<UserIdentityDto, TInputParameterDto> command,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto;

    /// <summary>
    /// Get orchestration instance by id.
    /// </summary>
    Task<OrchestrationInstanceTypedDto<TInputParameterDto>> GetOrchestrationInstanceByIdAsync<TInputParameterDto>(
        GetOrchestrationInstanceByIdQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto;

    /// <summary>
    /// Get all orchestration instances filtered by their related orchestration definition name and version,
    /// and their lifecycle / termination states.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstanceTypedDto<TInputParameterDto>>> SearchOrchestrationInstancesByNameAsync<TInputParameterDto>(
        SearchOrchestrationInstancesByNameQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto;

    /// <summary>
    /// Get all orchestration instances filtered by a custom query which at least filters by name.
    /// </summary>
    Task<IReadOnlyCollection<OrchestrationInstanceTypedDto<TInputParameterDto>>> SearchOrchestrationInstancesByNameAsync<TInputParameterDto>(
        SearchOrchestrationInstancesByCustomQuery query,
        CancellationToken cancellationToken)
            where TInputParameterDto : IInputParameterDto;
}
