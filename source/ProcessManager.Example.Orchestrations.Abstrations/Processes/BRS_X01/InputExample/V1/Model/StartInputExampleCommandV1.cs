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

namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.InputExample.V1.Model;

/// <summary>
/// Command for starting the BRS_X01.
/// Must be JSON serializable.
/// </summary>
public record StartInputExampleCommandV1
    : StartOrchestrationInstanceCommand<UserIdentityDto, InputV1>
{
    /// <summary>
    /// Construct command.
    /// </summary>
    /// <param name="operatingIdentity">Identity of the user executing the command.</param>
    /// <param name="inputParameter"> contains the Durable Functions orchestration input parameter value.</param>
    public StartInputExampleCommandV1(
        UserIdentityDto operatingIdentity,
        InputV1 inputParameter)
            : base(
                operatingIdentity,
                orchestrationDescriptionUniqueName: new Brs_X01_InputExample_V1(),
                inputParameter)
    {
    }
}