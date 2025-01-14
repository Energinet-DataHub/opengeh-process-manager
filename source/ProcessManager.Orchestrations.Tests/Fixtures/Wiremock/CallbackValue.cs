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

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Wiremock;

/// <summary>
/// Class to hold a value that can be set and retrieved as a callback.
/// This is useful for testing with wiremock, and the like,
/// since we may change the output of <see cref="GetValue"/> via <see cref="SetValue"/> while running the test.
/// </summary>
public class CallbackValue<TValue>(
    TValue? value)
{
    private TValue? _value = value;

    /// <summary>
    /// Get the value set by <see cref="SetValue"/>.
    /// </summary>
    public TValue? GetValue() => _value;

    /// <summary>
    /// Sets the value which will be returned by <see cref="GetValue"/>.
    /// </summary>
    public void SetValue(TValue newValue) => _value = newValue;
}
