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

using System.ComponentModel.DataAnnotations;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.Extensions.Options;

/// <summary>
/// Contains Process Manager options that we can configure as hierarchical.
/// </summary>
public class ProcessManagerOptions
{
    public const string SectionName = "ProcessManager";

    /// <summary>
    /// Connection string to the SQL database used by Process Manager components.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string SqlDatabaseConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Allow the orchestration register to update the orchestration description with breaking changes.
    /// </summary>
    [Required]
    public bool AllowOrchestrationDescriptionBreakingChanges { get; set; } = false;

    /// <summary>
    /// Allow the orchestration instance manager to start/schedule orchestration descriptions under development.
    /// </summary>
    [Required]
    public bool AllowStartingOrchestrationsUnderDevelopment { get; set; } = true;
}
