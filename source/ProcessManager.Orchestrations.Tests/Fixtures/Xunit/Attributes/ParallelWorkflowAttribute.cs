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

using Xunit.Sdk;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.Attributes;

/// <summary>
/// Apply attribute on xUnit test class for tests to specify in which
/// workflow bucket the containing tests should be executed. Each
/// bucket should be executed on its own GitHub runner.
///
/// GitHub workflows should then use xUnit trait filter expressions to
/// execute tests accordingly.
///
/// See xUnit filter possibilities: https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests?pivots=xunit
/// </summary>
[TraitDiscoverer(
    typeName: "Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.TraitDiscoverers.ParallelWorkflowDiscoverer",
    assemblyName: "Energinet.DataHub.ProcessManager.Orchestrations.Tests")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ParallelWorkflowAttribute : Attribute, ITraitAttribute
{
    public ParallelWorkflowAttribute(
        WorkflowBucket bucket = WorkflowBucket.Default)
    {
    }
}

/// <summary>
/// Workflow bucket filter.
/// </summary>
#pragma warning disable SA1201 // Elements should appear in the correct order
public enum WorkflowBucket
#pragma warning restore SA1201 // Elements should appear in the correct order
{
    Default,
    Bucket01,
    Bucket02,
    Bucket03,
    Bucket04,
    Bucket05,
}
