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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

/// <summary>
/// Interface for commands the require <see cref="OrchestrationDescriptionUniqueName"/>
/// and an input parameter.
/// Allows us the implement general handling of implementing commands.
/// </summary>
/// <typeparam name="TInputParameterDto">The input parameter type. Must be a JSON serializable type.</typeparam>
public interface IOrchestrationDescriptionCommand<out TInputParameterDto>
    where TInputParameterDto : IInputParameterDto
{
    /// <summary>
    /// Uniquely identifies the orchestration description from which the
    /// orchestration instance should be created.
    /// </summary>
    OrchestrationDescriptionUniqueNameDto OrchestrationDescriptionUniqueName { get; }

    /// <summary>
    /// Contains the Durable Functions orchestration input parameter value.
    /// </summary>
    TInputParameterDto InputParameter { get; }
}

/// <summary>
/// Interface for commands the require <see cref="OrchestrationDescriptionUniqueName"/>.
/// Allows us the implement general handling of implementing commands.
/// </summary>
public interface IOrchestrationDescriptionCommand
{
    /// <summary>
    /// Uniquely identifies the orchestration description from which the
    /// orchestration instance should be created.
    /// </summary>
    OrchestrationDescriptionUniqueNameDto OrchestrationDescriptionUniqueName { get; }
}
