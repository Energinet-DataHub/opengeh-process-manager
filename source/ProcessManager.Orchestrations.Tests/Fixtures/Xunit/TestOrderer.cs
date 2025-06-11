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

using Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.TraitDiscoverers;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit;

public class TestOrderer : ITestCaseOrderer
{
    public const string TypeName = "Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Xunit.TestOrderer";
    public const string AssemblyName = "Energinet.DataHub.ProcessManager.Orchestrations.Tests";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : notnull, ITestCase
    {
        return testCases.OrderBy(GetOrderTrait).ToList();
    }

    private static int GetOrderTrait<TTestCase>(TTestCase testCase)
        where TTestCase : notnull, ITestCase
    {
        testCase.Traits.TryGetValue(TestOrderDiscoverer.TraitName, out var orderTrait);

        if (orderTrait == null)
            return 0;

        var orderTraitValue = int.Parse(orderTrait.First());
        return orderTraitValue;
    }
}
