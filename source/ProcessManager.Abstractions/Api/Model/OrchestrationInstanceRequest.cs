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
/// An orchestration instance request executed by an identity.
/// Must be JSON serializable.
/// </summary>
/// <typeparam name="TOperatingIdentity">The operating identity type. Must be a JSON serializable type.</typeparam>
/// <param name="OperatingIdentity">The identity executing the request.</param>
public abstract record OrchestrationInstanceRequest<TOperatingIdentity>(
    TOperatingIdentity OperatingIdentity)
        : IOrchestrationInstanceRequest
            where TOperatingIdentity : IOperatingIdentityDto;
