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

using System.Security.Cryptography;
using System.Text;

namespace Energinet.DataHub.ProcessManager.Core.Domain.OrchestrationInstance;

public record IdempotencyKey(string Value)
{
    /// <summary>
    /// Hash the idempotency key using SHA-256 and return the hash as a byte array, which is exactly 32 bytes.
    /// </summary>
    public byte[] ToHash()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(Value));
    }

    /// <summary>
    /// Create a new idempotency key based on <see cref="Guid.NewGuid()"/>.
    /// </summary>
    public static IdempotencyKey CreateNew() => new IdempotencyKey(Guid.NewGuid().ToString());
}
