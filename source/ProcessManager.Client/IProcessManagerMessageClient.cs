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

namespace Energinet.DataHub.ProcessManager.Client;

/// <summary>
/// Client for using the Process Manager / Orchestrations API.
/// </summary>
public interface IProcessManagerMessageClient
{
    /// <summary>
    /// Start an orchestration instance.
    /// </summary>
    Task StartNewOrchestrationInstanceAsync<TInputParameterDto>(
        StartOrchestrationInstanceMessageCommand<TInputParameterDto> command,
        CancellationToken cancellationToken)
            where TInputParameterDto : class, IInputParameterDto;

    /// <summary>
    /// Send a notify event to an orchestration instance.
    /// </summary>
    Task NotifyOrchestrationInstanceAsync(
        NotifyOrchestrationInstanceEvent notifyEvent,
        CancellationToken cancellationToken);

    /// <summary>
    /// Send a notify event (with data) to an orchestration instance.
    /// </summary>
    Task NotifyOrchestrationInstanceAsync<TNotifyDataDto>(
        NotifyOrchestrationInstanceEvent<TNotifyDataDto> notifyEvent,
        CancellationToken cancellationToken)
        where TNotifyDataDto : class, INotifyDataDto;
}
