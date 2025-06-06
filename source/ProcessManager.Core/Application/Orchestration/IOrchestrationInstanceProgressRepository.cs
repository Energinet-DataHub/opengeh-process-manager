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

using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

namespace Energinet.DataHub.ProcessManager.Core.Application.Orchestration;

/// <summary>
/// Use this from Durable Functions activities to get the orchestration instance and then
/// update its progress, before commiting changes back by using <see cref="UnitOfWork"/>.
/// </summary>
public interface IOrchestrationInstanceProgressRepository
{
    /// <summary>
    /// Use <see cref="IUnitOfWork.CommitAsync"/> to save changes.
    /// </summary>
    IUnitOfWork UnitOfWork { get; }

    /// <summary>
    /// Get existing orchestration instance.
    /// To commit changes use <see cref="UnitOfWork"/>.
    /// </summary>
    Task<OrchestrationInstance> GetAsync(OrchestrationInstanceId id);
}
