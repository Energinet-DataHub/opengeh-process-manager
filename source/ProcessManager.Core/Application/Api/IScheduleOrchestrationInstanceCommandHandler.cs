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

namespace Energinet.DataHub.ProcessManagement.Core.Application.Api;

/// <summary>
/// Interface for handling a command for scheduling an orchestration instance with an input parameter.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TInputParameterDto">The type of the input parameter DTO.</typeparam>
public interface IScheduleOrchestrationInstanceCommandHandler<TCommand, TInputParameterDto>
    where TCommand : ScheduleOrchestrationInstanceCommand<TInputParameterDto>
    where TInputParameterDto : IInputParameterDto
{
    /// <summary>
    /// Handles a command for scheduling an orchestration instance.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <returns>The ID of the orchestration instance.</returns>
    Task<Guid> HandleAsync(TCommand command);
}
