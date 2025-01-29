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

using Energinet.DataHub.ProcessManager.Shared.Tests.Fixtures;
using Moq;
using NodaTime;

namespace Energinet.DataHub.ProcessManager.Tests.Fixtures;

public class SchedulerHandlerFixture : IAsyncLifetime
{
    public SchedulerHandlerFixture()
    {
        ClockMock = CreateClockMock();

        DatabaseManager = new ProcessManagerDatabaseManager(nameof(SchedulerHandlerFixture));
    }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    /// <summary>
    /// Clock is used by the handler to get the UTC time, so we mock it to return a static UTC time.
    /// </summary>
    public Mock<IClock> ClockMock { get; }

    public async Task InitializeAsync()
    {
        await DatabaseManager.CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await DatabaseManager.DeleteDatabaseAsync();
    }

    private Mock<IClock> CreateClockMock()
    {
        var utc9AM = Instant.FromUtc(2024, 12, 1, hourOfDay: 9, 0);

        var mock = new Mock<IClock>();
        mock.Setup(m => m.GetCurrentInstant())
            .Returns(utc9AM);

        return mock;
    }
}
