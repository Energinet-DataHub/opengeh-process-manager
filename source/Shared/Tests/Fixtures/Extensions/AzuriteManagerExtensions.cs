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

using Azure.Storage.Blobs;
using Energinet.DataHub.Core.FunctionApp.TestCommon.Azurite;
using Energinet.DataHub.ProcessManager.Core.Domain.SendMeasurements;

namespace Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures.Extensions;

public static class AzuriteManagerExtensions
{
    public static async Task CreateRequiredContainersAsync(this AzuriteManager azuriteManager)
    {
        List<string> containers = [SendMeasurementsInputFileStorageReference.ContainerName];

        var blobServiceClient = new BlobServiceClient(azuriteManager.BlobStorageConnectionString);
        foreach (var containerName in containers)
        {
            var container = blobServiceClient.GetBlobContainerClient(containerName);
            var containerExists = await container.ExistsAsync();

            if (!containerExists.Value)
                await container.CreateAsync();
        }
    }
}
