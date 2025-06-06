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

using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationDescription;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Abstractions.Api.Model;

/// <summary>
/// Command for starting an orchestration instance.
/// Must be JSON serializable.
/// </summary>
/// <typeparam name="TOperatingIdentity">The operating identity type. Must be a JSON serializable type.</typeparam>
public abstract record StartOrchestrationInstanceCommand<TOperatingIdentity>
    : OrchestrationInstanceRequest<TOperatingIdentity>,
    IOrchestrationDescriptionCommand
        where TOperatingIdentity : IOperatingIdentityDto
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity executing the command.</param>
    /// <param name="orchestrationDescriptionUniqueName">Uniquely identifies the orchestration description from which the
    /// orchestration instance should be created.</param>
    public StartOrchestrationInstanceCommand(
        TOperatingIdentity operatingIdentity,
        OrchestrationDescriptionUniqueNameDto orchestrationDescriptionUniqueName)
            : base(operatingIdentity)
    {
        OrchestrationDescriptionUniqueName = orchestrationDescriptionUniqueName;
    }

    /// <inheritdoc/>
    public OrchestrationDescriptionUniqueNameDto OrchestrationDescriptionUniqueName { get; }
}

/// <summary>
/// Command for starting an orchestration instance with an input parameter.
/// Must be JSON serializable.
/// </summary>
/// <typeparam name="TOperatingIdentity">The operating identity type. Must be a JSON serializable type.</typeparam>
/// <typeparam name="TInputParameterDto">The input parameter type. Must be a JSON serializable type.</typeparam>
public abstract record StartOrchestrationInstanceCommand<TOperatingIdentity, TInputParameterDto>
    : StartOrchestrationInstanceCommand<TOperatingIdentity>,
    IOrchestrationDescriptionCommand<TInputParameterDto>
        where TOperatingIdentity : IOperatingIdentityDto
        where TInputParameterDto : IInputParameterDto
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity executing the command.</param>
    /// <param name="orchestrationDescriptionUniqueName">Uniquely identifies the orchestration description from which the
    /// orchestration instance should be created.</param>
    /// <param name="inputParameter">Contains the Durable Functions orchestration input parameter value.</param>
    public StartOrchestrationInstanceCommand(
        TOperatingIdentity operatingIdentity,
        OrchestrationDescriptionUniqueNameDto orchestrationDescriptionUniqueName,
        TInputParameterDto inputParameter)
            : base(operatingIdentity, orchestrationDescriptionUniqueName)
    {
        InputParameter = inputParameter;
    }

    /// <inheritdoc/>
    public TInputParameterDto InputParameter { get; }
}
