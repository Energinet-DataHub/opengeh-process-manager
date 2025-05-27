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

using Azure;
using Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;

namespace Energinet.DataHub.ProcessManager.Core.Application.FileStorage;

public interface IFileStorageClient
{
    /// <summary>
    /// Upload a stream using the reference parameter as a unique representation.
    /// </summary>
    /// <param name="reference">A <see cref="FileStorageReference"/> representing the file, which can have any string value. If a file already exists with the given reference, a <see cref="RequestFailedException" /> will be thrown.</param>
    /// <param name="stream">A stream which contains the binary file.</param>
    Task UploadAsync(FileStorageReference reference, Stream stream);

    /// <summary>
    /// Downloads a file as a stream, found by the given reference string.
    /// <remarks>The stream is downloaded when read, and can only be read once</remarks>
    /// </summary>
    /// <param name="reference">The reference string is used to determine which file to download.</param>
    /// <param name="cancellationToken"></param>
    Task<ReadOnceStream> DownloadAsync(FileStorageReference reference, CancellationToken cancellationToken);
}
