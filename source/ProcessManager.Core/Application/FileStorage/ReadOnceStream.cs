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

namespace Energinet.DataHub.ProcessManager.Core.Application.FileStorage;

/// <summary>
/// Wraps a stream that can only be read once, and protects it against being read multiple times.
/// </summary>
public class ReadOnceStream
{
    private readonly Stream _stream;

    private ReadOnceStream(Stream stream)
    {
        _stream = stream;
    }

    public bool CanRead { get; private set; } = true;

    public Stream Stream
    {
        get
        {
            if (!CanRead)
                throw new InvalidOperationException("Trying to read a stream that has already been read. ReadOnceStream can only be read once.");

            CanRead = false;
            return _stream;
        }
    }

    public static ReadOnceStream Create(Stream stream)
    {
        return new ReadOnceStream(stream);
    }
}
