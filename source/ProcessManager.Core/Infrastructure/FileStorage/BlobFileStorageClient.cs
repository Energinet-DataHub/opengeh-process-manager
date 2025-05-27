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
using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;
using Microsoft.Extensions.Azure;

namespace Energinet.DataHub.ProcessManager.Core.Infrastructure.FileStorage;

public class BlobFileStorageClient(
    IAzureClientFactory<BlobServiceClient> blobServiceClientFactory)
        : IFileStorageClient
{
    public const string ClientName = "BlobFileStorageClient";

    private readonly BlobServiceClient _blobServiceClient = blobServiceClientFactory.CreateClient(ClientName);

    /// <inheritdoc />
    public Task UploadAsync(FileStorageReference reference, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(reference);

        var container = _blobServiceClient.GetBlobContainerClient(reference.Category);
        stream.Position = 0; // Make sure we read the entire stream

        return container.UploadBlobAsync(reference.Path, stream);
    }

    /// <inheritdoc />
    public async Task<ReadOnceStream> DownloadAsync(FileStorageReference reference, CancellationToken cancellationToken)
    {
        var container = _blobServiceClient.GetBlobContainerClient(reference.Category);
        var blob = container.GetBlobClient(reference.Path);

        // OpenReadAsync() returns a stream for the file, and the file is downloaded the first time the stream is read
        var downloadStream = await blob.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return ReadOnceStream.Create(downloadStream);
    }
}
