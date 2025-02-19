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

namespace Energinet.DataHub.ProcessManager.Client.Extensions.Options;

/// <summary>
/// Options for configuration of Process Manager Service Bus clients using the Process Manager.
/// </summary>
public class ProcessManagerServiceBusClientOptions
{
    public const string SectionName = "ProcessManagerServiceBusClient";

    /// <summary>
    /// Name of the topic which the Process Manager receives service bus messages on
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string TopicName { get; set; } = string.Empty;
}
