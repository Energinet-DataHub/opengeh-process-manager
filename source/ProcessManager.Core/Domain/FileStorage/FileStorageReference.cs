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

using Energinet.DataHub.ProcessManager.Abstractions.Core.ValueObjects;
using Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Core.Domain.FileStorage;

public abstract record FileStorageReference
{
    /// <summary>
    /// Path must be supported by Azure File Storage, so there should be no "-" or other unsupported characters.
    /// </summary>
    public abstract string Path { get; }

    /// <summary>
    /// Category is used for the file storage container name, and must be all lowercase.
    /// </summary>
    public abstract string Category { get; }
}
