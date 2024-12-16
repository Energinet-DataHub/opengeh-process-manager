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

using System.Dynamic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Energinet.DataHub.Core.TestCommon;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using Energinet.DataHub.ProcessManager.Client.Tests.Fixtures;
using FluentAssertions;
using Xunit.Abstractions;

namespace Energinet.DataHub.ProcessManager.Client.Tests.Integration.BRS_X01.V1;

/// <summary>
/// Test case where we verify the Process Manager clients can be used to start a
/// calculation orchestration (with input parameter) and monitor its status during its lifetime.
/// </summary>
[Collection(nameof(ProcessManagerExampleClientCollection))]
public class MonitorExampleUsingApiScenario : IAsyncLifetime
{
    public MonitorExampleUsingApiScenario(
        ProcessManagerExampleClientFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private ProcessManagerExampleClientFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ProcessManagerAppManager.AppHostManager.ClearHostLog();
        Fixture.OrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.ProcessManagerAppManager.SetTestOutputHelper(null!);
        Fixture.OrchestrationsAppManager.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Calculation_WhenStarted_CanMonitorLifecycle()
    {
        // TODO: Move to API test project
        dynamic scheduleRequestDto = new ExpandoObject();
        scheduleRequestDto.OperatingIdentity = new ExpandoObject();
        scheduleRequestDto.OperatingIdentity.UserId = Guid.NewGuid();
        scheduleRequestDto.OperatingIdentity.ActorId = Guid.NewGuid();
        scheduleRequestDto.OrchestrationDescriptionUniqueName = new ExpandoObject();
        scheduleRequestDto.OrchestrationDescriptionUniqueName.Name = "BRS_023_027";
        scheduleRequestDto.OrchestrationDescriptionUniqueName.Version = 1;
        scheduleRequestDto.InputParameter = new ExpandoObject();
        scheduleRequestDto.InputParameter.CalculationType = 0;
        scheduleRequestDto.InputParameter.GridAreaCodes = new[] { "543" };
        scheduleRequestDto.InputParameter.PeriodStartDate = "2024-10-29T15:19:10.0151351+01:00";
        scheduleRequestDto.InputParameter.PeriodEndDate = "2024-10-29T16:19:10.0193962+01:00";
        scheduleRequestDto.InputParameter.IsInternalCalculation = true;

        using var scheduleRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/orchestrationinstance/command/start/custom/brs_023_027/1");
        scheduleRequest.Content = new StringContent(
            JsonSerializer.Serialize(scheduleRequestDto),
            Encoding.UTF8,
            "application/json");

        // Step 1: Start new calculation orchestration instance
        using var scheduleResponse = await Fixture.OrchestrationsAppManager.AppHostManager
            .HttpClient
            .SendAsync(scheduleRequest);
        scheduleResponse.EnsureSuccessStatusCode();

        var calculationId = await scheduleResponse.Content
            .ReadFromJsonAsync<Guid>();

        // Step 2: Query until terminated with succeeded
        dynamic queryRequestDto = new ExpandoObject();
        queryRequestDto.OperatingIdentity = new ExpandoObject();
        queryRequestDto.OperatingIdentity.UserId = Guid.NewGuid();
        queryRequestDto.OperatingIdentity.ActorId = Guid.NewGuid();
        queryRequestDto.Id = calculationId;

        var isTerminated = await Awaiter.TryWaitUntilConditionAsync(
            async () =>
            {
                using var queryRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    "/api/orchestrationinstance/query/id");
                queryRequest.Content = new StringContent(
                    JsonSerializer.Serialize(queryRequestDto),
                    Encoding.UTF8,
                    "application/json");

                using var queryResponse = await Fixture.ProcessManagerAppManager.AppHostManager
                    .HttpClient
                    .SendAsync(queryRequest);
                queryResponse.EnsureSuccessStatusCode();

                var orchestrationInstance = await queryResponse.Content
                    .ReadFromJsonAsync<OrchestrationInstanceDto>();

                return orchestrationInstance!.Lifecycle.State == OrchestrationInstanceLifecycleStates.Terminated;
            },
            timeLimit: TimeSpan.FromSeconds(40),
            delay: TimeSpan.FromSeconds(2));

        isTerminated.Should().BeTrue("because we expects the orchestration instance can complete within given wait time");
    }
}
