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
/// This interface is nexessary to be able to use "out TInputParameterDto".
/// </summary>
/// <typeparam name="TInputParameterDto">Must be a JSON serializable type.</typeparam>
public interface IOrchestrationInstanceTypedDto<out TInputParameterDto>
    where TInputParameterDto : class, IInputParameterDto
{
    /// <summary>
    /// The id of the orchestration instance.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The life cycle state of the orchestration instance.
    /// </summary>
    OrchestrationInstanceLifecycleDto Lifecycle { get; }

    /// <summary>
    /// The steps of the orchestration instance.
    /// </summary>
    IReadOnlyCollection<StepInstanceDto> Steps { get; }

    /// <summary>
    /// The parameter value.
    /// </summary>
    TInputParameterDto ParameterValue { get; }
}
