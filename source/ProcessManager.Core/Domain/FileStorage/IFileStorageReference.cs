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

namespace Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;

/// <summary>
/// A reference to a file in the file storage. Represented by a path to the file and the file's category.
/// </summary>
public interface IFileStorageReference
{
    /// <summary>
    /// The path to the file.
    /// </summary>
    /// <remarks>If using Azure File Storage, the path must be valid (shouldn't contain "-" characters).</remarks>
    string Path { get; }

    /// <summary>
    /// The file category, used to group files in file storage.
    /// </summary>
    /// <remarks>If using Azure File Storage, the category represents the Container in Azure Blob Storage.</remarks>
    string Category { get; }
}
