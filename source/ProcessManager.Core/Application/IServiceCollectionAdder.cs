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

using Microsoft.Extensions.DependencyInjection;

namespace Energinet.DataHub.ProcessManager.Core.Application;

/// <summary>
/// This interface is used to add services to the service collection.
/// It's done via the method "RuntimeHelpers.GetUninitializedObject(t)" hence it may never have a constructor.
/// </summary>
public interface IServiceCollectionAdder
{
    /// <summary>
    /// Some implementations of this interface will add services to the service collection.
    /// </summary>
    IServiceCollection Add(IServiceCollection services);
}
