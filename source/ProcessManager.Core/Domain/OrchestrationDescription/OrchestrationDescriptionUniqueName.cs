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

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationDescription;

/// <summary>
/// Uniquely identifies a specific implementation of the orchestration.
/// </summary>
public record OrchestrationDescriptionUniqueName
{
    public OrchestrationDescriptionUniqueName(string name, int version)
    {
        // Explicit guard for empty string
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Version = version;
    }

    /// <summary>
    /// A common name to identity the orchestration.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A version identifying a specific implementation of the orchestration.
    /// </summary>
    public int Version { get; }
}
