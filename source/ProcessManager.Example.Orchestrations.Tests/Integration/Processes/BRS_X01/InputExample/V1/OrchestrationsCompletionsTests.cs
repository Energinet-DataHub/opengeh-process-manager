﻿// // Copyright 2020 Energinet DataHub A/S
// //
// // Licensed under the Apache License, Version 2.0 (the "License2");
// // you may not use this file except in compliance with the License.
// // You may obtain a copy of the License at
// //
// //     http://www.apache.org/licenses/LICENSE-2.0
// //
// // Unless required by applicable law or agreed to in writing, software
// // distributed under the License is distributed on an "AS IS" BASIS,
// // WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// // See the License for the specific language governing permissions and
// // limitations under the License.
//
// using System.Text;
// using System.Text.Json;
// using Energinet.DataHub.Core.DurableFunctionApp.TestCommon.DurableTask;
// using Energinet.DataHub.ProcessManager.Example.Orchestrations.Abstractions.Processes.BRS_X01.Example.V1.Model;
// using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Fixtures;
// using Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Models;
// using Energinet.DataHub.ProcessManager.Abstractions.Api.Model.OrchestrationInstance;
// using FluentAssertions;
// using Xunit.Abstractions;
//
// namespace Energinet.DataHub.ProcessManager.Example.Orchestrations.Tests.Integration.Processes.BRS_X01.Examples.V1;
//
// /// <summary>
// /// Tests that verify that we can start and finish a "BRS_X01" orchestration.
// /// </summary>
// [Collection(nameof(ExampleOrchestrationsAppCollection))]
// public class OrchestrationsCompletionsTests : IAsyncLifetime
// {
//     public OrchestrationsCompletionsTests(ExampleOrchestrationsAppFixture fixture, ITestOutputHelper testOutputHelper)
//     {
//         Fixture = fixture;
//         Fixture.SetTestOutputHelper(testOutputHelper);
//     }
//
//     private ExampleOrchestrationsAppFixture Fixture { get; }
//
//     public Task InitializeAsync()
//     {
//         Fixture.ExampleOrchestrationsAppManager.AppHostManager.ClearHostLog();
//
//         return Task.CompletedTask;
//     }
//
//     public Task DisposeAsync()
//     {
//         Fixture.SetTestOutputHelper(null!);
//
//         return Task.CompletedTask;
//     }
//
//     [Fact]
//     public async Task ExampleOrchestration_WhenOrchestrationIsStarted_ThenItCompletesAndHaveHistoryCount8()
//     {
//         // Arrange
//         var input = new InputV1(false);
//
//         // Act
//         var orchestrationId = await StartOrchestrationAsync(input);
//
//         // Assert
//         // => Wait for orchestration to complete
//         var completedOrchestrationStatus = await Fixture.ExampleOrchestrationsAppManager.DurableClient.WaitForOrchestrationCompletedAsync(
//             orchestrationId,
//             TimeSpan.FromSeconds(60));
//         completedOrchestrationStatus.Should().NotBeNull();
//
//         var activities = completedOrchestrationStatus.History
//             .OrderBy(item => item["Timestamp"])
//             .Select(item => item.ToObject<OrchestrationHistoryItem>())
//             .ToList();
//
//         activities.Should()
//             .NotBeNull()
//             .And.Equal(
//             [
//                 new OrchestrationHistoryItem(
//                     EventType: "ExecutionStarted",
//                     Name: null,
//                     FunctionName: "Orchestration_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     EventType: "TaskCompleted",
//                     Name: null,
//                     FunctionName: "InitializeOrchestrationActivity_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     EventType: "TaskCompleted",
//                     Name: null,
//                     FunctionName: "FirstStepStartActivity_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     EventType: "TaskCompleted",
//                     Name: null,
//                     FunctionName: "FirstStepStopActivity_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     EventType: "TaskCompleted",
//                     Name: null,
//                     FunctionName: "SecondStepStartActivity_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     EventType: "TaskCompleted",
//                     Name: null,
//                     FunctionName: "SecondStepStopActivity_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     EventType: "TaskCompleted",
//                     Name: null,
//                     FunctionName: "TerminateOrchestrationActivity_Brs_X01_Example_V1"),
//                 new OrchestrationHistoryItem(
//                     "ExecutionCompleted",
//                     null,
//                     null),
//             ]);
//     }
//
//     private async Task<string> StartOrchestrationAsync(InputV1 input)
//     {
//         var command = new StartExampleCommandV1(
//             operatingIdentity: new UserIdentityDto(
//                 Guid.NewGuid(),
//                 Guid.NewGuid()),
//             input);
//
//         var json = JsonSerializer.Serialize(command, command.GetType());
//
//         using var request = new HttpRequestMessage(
//             HttpMethod.Post,
//             $"/api/orchestrationinstance/command/start/custom/{command.OrchestrationDescriptionUniqueName.Name}/{command.OrchestrationDescriptionUniqueName.Version}");
//
//         request.Content = new StringContent(
//             json,
//             Encoding.UTF8,
//             "application/json");
//
//         var id = await (await Fixture.ExampleOrchestrationsAppManager.AppHostManager.HttpClient
//                 .SendAsync(request))
//             .Content.ReadAsStringAsync();
//
//         // id is a string containing a start and end quote :(
//         return id.Substring(1, id.Length - 2);
//     }
// }
