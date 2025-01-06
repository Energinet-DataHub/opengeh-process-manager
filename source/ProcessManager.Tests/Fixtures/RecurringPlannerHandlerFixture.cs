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

public class RecurringPlannerHandlerFixture : IAsyncLifetime
{
    public RecurringPlannerHandlerFixture()
    {
        DkTimeZone = DateTimeZoneProviders.Tzdb["Europe/Copenhagen"];
        ClockMock = CreateClockMock();

        var time1200 = new LocalTime(12, 0);
        var time1700 = new LocalTime(17, 0);
        var firstOfDecember2024 = new LocalDate(2024, 12, 1);
        var dateTime1200 = firstOfDecember2024.At(time1200);
        var dateTime1700 = firstOfDecember2024.At(time1700);

        DkFirstOfDecember2024At1200 = dateTime1200.InZoneLeniently(DkTimeZone);
        DkFirstOfDecember2024At1700 = dateTime1700.InZoneLeniently(DkTimeZone);

        DatabaseManager = new ProcessManagerDatabaseManager(nameof(RecurringPlannerHandlerFixture));
    }

    public ProcessManagerDatabaseManager DatabaseManager { get; }

    /// <summary>
    /// The time zone information is used by the handler to convert the UTC time to a local time.
    /// </summary>
    public DateTimeZone DkTimeZone { get; }

    /// <summary>
    /// Clock is used by the handler to get the UTC time, so we mock it to return a static UTC time.
    /// </summary>
    public Mock<IClock> ClockMock { get; }

    public ZonedDateTime DkFirstOfDecember2024At1200 { get; }

    public ZonedDateTime DkFirstOfDecember2024At1700 { get; }

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
