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

using Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.Measurements.Model;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Processes.BRS_021.SendMeasurements.Measurements;

public interface IMeasurementsClient
{
    /// <summary>
    /// Responsible for sending measurements for a metering point to Measurements subsytem.
    /// </summary>
    /// <param name="measurementsForMeteringPoint"></param>
    /// <param name="cancellationToken"></param>
    Task SendAsync(MeasurementsForMeteringPoint measurementsForMeteringPoint, CancellationToken cancellationToken);
}
