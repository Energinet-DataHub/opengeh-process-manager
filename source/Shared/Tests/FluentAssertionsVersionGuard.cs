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

using FluentAssertions;
using Xunit;

namespace Energinet.DataHub.ProcessManager.Tests;

public class FluentAssertionsVersionGuard
{
    /// <summary>
    /// FluentAssertions have from version 8 introduced a new license model.
    /// This guard ensures that we do not use a version that is not allowed.
    /// </summary>
    [Fact]
    public void ShouldHaveValidVersion()
    {
        var actualVersion = typeof(FluentAssertions.ObjectAssertionsExtensions).Assembly.GetName().Version;
        var forbiddenVersion = new Version(8, 0, 0, 0);

        actualVersion.Should().NotBeNull();
        actualVersion.Should().BeLessThan(forbiddenVersion, "FluentAssertions >= 8 is not allowed.");
    }
}
