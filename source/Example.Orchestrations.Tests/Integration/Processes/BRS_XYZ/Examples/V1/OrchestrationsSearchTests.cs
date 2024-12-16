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

using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_XYZ.Example.V1;
using Energinet.DataHub.Example.Orchestrations.Abstractions.Processes.BRS_XYZ.Example.V1.Model;
using Energinet.DataHub.Example.Orchestrations.Processes.BRS_XYZ.Example.V1;
using Energinet.DataHub.Example.Orchestrations.Tests.Fixtures;
using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
using FluentAssertions;
using NodaTime;
using Xunit.Abstractions;

namespace Energinet.DataHub.Example.Orchestrations.Tests.Integration.Processes.BRS_XYZ.Examples.V1;

/// <summary>
/// Tests that verify that we can search in "BRS_XYZ" orchestrations.
/// </summary>
[Collection(nameof(ExampleOrchestrationsAppCollection))]
public class OrchestrationsSearchTests : IAsyncLifetime
{
    public OrchestrationsSearchTests(ExampleOrchestrationsAppFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.SetTestOutputHelper(testOutputHelper);
    }

    private ExampleOrchestrationsAppFixture Fixture { get; }

    public Task InitializeAsync()
    {
        Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Fixture.SetTestOutputHelper(null!);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExampleOrchestration_WhenOrchestrationIsStarted_ThenItsSearchable()
    {
        // Arrange
        var input = new ExampleSkipStep(ShouldSkipStepTwo: false);
        await StartAndEnsureRunningAsync(input);

        var searchQuery = new ExampleQuery(
            operatingIdentity: new UserIdentityDto(
                Guid.NewGuid(),
                Guid.NewGuid()),
            skipStep: new ExampleSkipStep(ShouldSkipStepTwo: false));

        var searchJson = JsonSerializer.Serialize(searchQuery, searchQuery.GetType());

        // Act
        var actual = await SendSearchRequestAsync(searchJson);

        // Assert
        actual.Should().NotBeNull().And.NotBeEmpty();
    }

    private async Task<Collection<ExampleQueryResult>?> SendSearchRequestAsync(string search)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orchestrationinstance/query/custom/brs_xyz/example");

        request.Content = new StringContent(
            search,
            Encoding.UTF8,
            "application/json");
        var response = await Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient
            .SendAsync(request);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<Collection<ExampleQueryResult>>(content);
        return result;
    }

    private async Task<string> StartAndEnsureRunningAsync(ExampleSkipStep input)
    {
        var command = new StartCommand_Brs_Xyz_Example_V1(
            operatingIdentity: new UserIdentityDto(
                Guid.NewGuid(),
                Guid.NewGuid()),
            new Input_Brs_Xyz_Example_V1(input));

        var json = JsonSerializer.Serialize(command, command.GetType());

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orchestrationinstance/command/start/custom/{command.OrchestrationDescriptionUniqueName.Name}/{command.OrchestrationDescriptionUniqueName.Version}");

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        var id = await (await Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient
                .SendAsync(request))
            .Content.ReadAsStringAsync();

        var startedOrchestrationStatus = await Fixture.ExampleOrchestrationsAppManager.DurableClient.WaitForOrchestationStartedAsync(
            createdTimeFrom: SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromSeconds(5)).ToDateTimeUtc(),
            name: nameof(Orchestration_Brs_Xyz_Example_V1));
        startedOrchestrationStatus.Should().NotBeNull("The orchestration should have been started");

        // id is a string containing a start and end quote :(
        return id.Substring(1, id.Length - 2);
    }
}
