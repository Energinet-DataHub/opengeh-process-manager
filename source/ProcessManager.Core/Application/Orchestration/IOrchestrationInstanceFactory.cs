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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

/// <summary>
/// A custom designed interface for the feature Migrate Wholesale Calculations,
/// so we avoid exposing inner details to Orchestrations, and can easily remove
/// it again, when its done its purpose.
/// </summary>
public interface IOrchestrationInstanceFactory
{
    /// <summary>
    /// Create an Orchestration Instance from input.
    /// </summary>
    OrchestrationInstance CreateEntity();
}
