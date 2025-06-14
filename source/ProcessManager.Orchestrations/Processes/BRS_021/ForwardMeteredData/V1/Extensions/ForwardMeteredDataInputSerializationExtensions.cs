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

using System.Text.Json;
using Energinet.DataHub.ProcessManager.Core.Application.FileStorage;
using Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_021.ForwardMeteredData.V1.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.ForwardMeteredData.V1.Extensions;

internal static class ForwardMeteredDataInputSerializationExtensions
{
    public static async Task<MemoryStream> SerializeToStreamAsync(this ForwardMeteredDataInputV1 input)
    {
        var inputAsStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(inputAsStream, input).ConfigureAwait(false);

        return inputAsStream;
    }

    public static async Task<ForwardMeteredDataInputV1> DeserializeForwardMeteredDataInputV1Async(
        this ReadOnceStream inputStream,
        IFileStorageReference fileStorageReference)
    {
        var input = await JsonSerializer.DeserializeAsync<ForwardMeteredDataInputV1>(inputStream.Stream)
            .ConfigureAwait(false);

        return input
               ?? throw new InvalidOperationException($"Failed to deserialize input for SendMeasurementsInstance (Path={fileStorageReference.Path}, Container={fileStorageReference.Category}).");
    }
}
