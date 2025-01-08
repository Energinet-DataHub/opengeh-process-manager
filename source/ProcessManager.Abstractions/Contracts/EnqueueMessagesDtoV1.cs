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

namespace Energinet.DataHub.ProcessManager.Abstractions.Contracts;

public partial class EnqueueMessagesDtoV1
{
    public static string MajorVersion => nameof(EnqueueMessagesDtoV1);

    public static int MinorVersion => Descriptor.GetOptions().GetExtension(EnqueueMessagesDtoV1Extensions.MinorVersion);

    /// <summary>
    /// Get the major version, typically from the ApplicationProperties of a received service bus message.
    /// </summary>
    /// <param name="messageProperties">The message properties dictionary, typically from ApplicationProperties of a ServiceBusReceivedMessage.</param>
    /// <exception cref="ArgumentNullException">Throws an exception if the major version isn't found.</exception>
    public static string GetMajorVersion(IReadOnlyDictionary<string, object> messageProperties)
    {
        return (string?)messageProperties.GetValueOrDefault("MajorVersion")
               ?? throw new ArgumentNullException(
                   nameof(messageProperties),
                   "MajorVersion must be present in the ApplicationProperties of the received service bus message");
    }

    /// <summary>
    /// Get the minor version, typically from the ApplicationProperties of a received service bus message.
    /// </summary>
    /// <param name="messageProperties">The message properties dictionary, typically from ApplicationProperties of a ServiceBusReceivedMessage.</param>
    /// <exception cref="ArgumentNullException">Throws an exception if the minor version isn't found.</exception>
    public static int GetMinorVersion(IReadOnlyDictionary<string, object> messageProperties)
    {
        return (int?)messageProperties.GetValueOrDefault("MinorVersion")
               ?? throw new ArgumentNullException(
                   nameof(messageProperties),
                   "MinorVersion must be present in the ApplicationProperties of the received service bus message");
    }
}
