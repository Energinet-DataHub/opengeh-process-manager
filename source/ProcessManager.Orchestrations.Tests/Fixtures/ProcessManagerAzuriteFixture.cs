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

using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;

public class ProcessManagerAzuriteFixture : IAsyncLifetime
{
    public ProcessManagerAzuriteFixture()
    {
        AzuriteManager = new AzuriteManager(useOAuth: false, useSilentMode: true);
    }

    public AzuriteManager AzuriteManager { get; }

    public async Task InitializeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.StartAzurite();
        await AzuriteManager.CreateRequiredContainersAsync();
    }

    public async Task DisposeAsync()
    {
        AzuriteManager.CleanupAzuriteStorage();
        AzuriteManager.Dispose();

        await Task.CompletedTask;
    }
}
