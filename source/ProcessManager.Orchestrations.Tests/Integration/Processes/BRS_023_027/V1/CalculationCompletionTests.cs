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

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.ProcessManagement.Core.Domain.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Orchestrations.Abstractions.Processes.BRS_023_027.V1.Model;
using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures;
using FluentAssertions;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Integration.Processes.BRS_023_027.V1;

[Collection(nameof(OrchestrationsAppCollection))]
public class CalculationCompletionTests : IAsyncLifetime
{
    public CalculationCompletionTests(OrchestrationsAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private OrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Calculation_WhenStarted_CanMonitorLifecycle()
    {
        var input = new CalculationInputV1(
            CalculationTypes.WholesaleFixing,
            GridAreaCodes: new[] { "543" },
            PeriodStartDate: DateTimeOffset.Parse("2024-10-08T15:19:10.0151351+01:00"),
            PeriodEndDate: DateTimeOffset.Parse("2024-10-11T16:19:10.0193962+01:00"),
            IsInternalCalculation: false);

        var orchestrationId = await StartOrchestrationAsync(input);

        var completedOrchestrationStatus = await Fixture.OrchestrationsAppManager.DurableClient.WaitForOrchestrationCompletedAsync(
            orchestrationId.ToString(),
            TimeSpan.FromSeconds(60));
        completedOrchestrationStatus.Should().NotBeNull();

        await using var readDbContext = Fixture.OrchestrationsAppManager.DatabaseManager.CreateDbContext();
        var orchestrationInstance = await readDbContext.OrchestrationInstances.FindAsync();
        orchestrationInstance.Should().NotBeNull();
    }

    private async Task<OrchestrationInstanceId> StartOrchestrationAsync(CalculationInputV1 input)
    {
     var command = new StartCalculationCommandV1(
         operatingIdentity: new UserIdentityDto(
             Guid.NewGuid(),
             Guid.NewGuid()),
         input);

     var json = JsonSerializer.Serialize(command, command.GetType());

     using var request = new HttpRequestMessage(
         HttpMethod.Post,
         $"/api/orchestrationinstance/command/start/custom/{command.OrchestrationDescriptionUniqueName.Name}/{command.OrchestrationDescriptionUniqueName.Version}");

     request.Content = new StringContent(
         json,
         Encoding.UTF8,
         "application/json");

     var id = await (await Fixture.OrchestrationsAppManager.AppHostManager.HttpClient
             .SendAsync(request))
         .Content.ReadFromJsonAsync<Guid>();

     return new OrchestrationInstanceId(id);
    }
}
