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

using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Core.Application;

/// <summary>
/// Register options to the service container.
/// All implementations of this interface will be loaded during startup and
/// used to register options for Durable Function orchestrations.
/// </summary>
public interface IOptionsConfiguration
{
    /// <summary>
    /// Adds the specified services to the service collection.
    /// Please only add options this ways and register other services in "program.cs"
    /// </summary>
    IServiceCollection Configure(IServiceCollection services);
}
