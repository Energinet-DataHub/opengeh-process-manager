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

using System.Net;
using Microsoft.Azure.Databricks.Client.Models;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;

namespace Energinet.DataHub.ProcessManager.Orchestrations.Tests.Fixtures.Extensions;

/// <summary>
/// A collection of WireMock extensions for easy mock configuration of
/// Databricks REST API endpoints.
///
/// IMPORTANT developer tips:
///  - It's possible to start the WireMock server in Proxy mode, this means
///    that all requests are proxied to the real URL. And the mappings can be recorded and saved.
///    See https://github.com/WireMock-Net/WireMock.Net/wiki/Proxying
///  - WireMockInspector: https://github.com/WireMock-Net/WireMockInspector/blob/main/README.md
///  - WireMock.Net examples: https://github.com/WireMock-Net/WireMock.Net-examples
/// </summary>
public static class DatabricksApiWireMockExtensions
{
    public static WireMockServer MockJobsList(this WireMockServer server, long jobId, string jobName)
    {
        var request = Request
            .Create()
            .WithPath("/api/2.1/jobs/list")
            .WithParam("name", jobName)
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(BuildJobsListJson(jobId, jobName));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    public static WireMockServer MockJobsGet(this WireMockServer server, long jobId, string jobName)
    {
        var request = Request
            .Create()
            .WithPath("/api/2.1/jobs/get")
            .WithParam("job_id", jobId.ToString())
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(BuildJobsGetJson(jobId, jobName));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    public static WireMockServer MockJobsRunNow(this WireMockServer server, long runId)
    {
        var request = Request
            .Create()
            .WithPath("/api/2.1/jobs/run-now")
            .UsingPost();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(BuildJobsRunNowJson(runId));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    public static WireMockServer MockJobsRunsGet(this WireMockServer server, long runId, string jobName, RunStatusState lifeCycleState, RunTerminationCode resultState)
    {
        var request = Request
            .Create()
            .WithPath("/api/2.1/jobs/runs/get")
            .WithParam("run_id", runId.ToString())
            .UsingGet();

        var response = Response
            .Create()
            .WithStatusCode(HttpStatusCode.OK)
            .WithHeader(HeaderNames.ContentType, "application/json")
            .WithBody(BuildJobsRunsGetJson(runId, lifeCycleState, resultState));

        server
            .Given(request)
            .RespondWith(response);

        return server;
    }

    /// <summary>
    /// Creates a '/jobs/list' JSON response with exactly one job
    /// containing the given job id and job name.
    /// </summary>
    private static string BuildJobsListJson(long jobId, string jobName)
    {
        var json = """
                   {
                     "jobs": [
                       {
                         "job_id": {jobId},
                         "settings": {
                           "name": "{jobName}"
                         }
                       }
                     ],
                     "has_more": false
                   }
                   """;
        return json
            .Replace("{jobId}", jobId.ToString())
            .Replace("{jobName}", jobName);
    }

    /// <summary>
    /// Creates a '/jobs/get' JSON response with the given job id and job name.
    /// </summary>
    private static string BuildJobsGetJson(long jobId, string jobName)
    {
        var json = """
                   {
                     "job_id": {jobId},
                     "settings": {
                       "name": "{jobName}"
                     }
                   }
                   """;

        return json
            .Replace("{jobId}", jobId.ToString())
            .Replace("{jobName}", jobName);
    }

    /// <summary>
    /// Creates a '/jobs/run-now' JSON response with the given run id.
    /// </summary>
    private static string BuildJobsRunNowJson(long runId)
    {
        var json = """
                   {
                     "run_id": {runId}
                   }
                   """;

        return json.Replace("{runId}", runId.ToString());
    }

    /// <summary>
    /// Creates a '/jobs/runs/get' JSON response with the given job id (run id).
    /// </summary>
    private static string BuildJobsRunsGetJson(long jobId, RunStatusState lifeCycleState, RunTerminationCode resultState)
    {
        var json = """
                   {
                     "job_id": {jobId},
                     "status": {
                       "state": "{lifeCycleState}",
                       "termination_details": {
                            "code": "{resultState}",
                            "type": "CLOUD_FAILURE",
                            "message": "Some arbitrary message"
                       }
                     }
                   }
                   """;

        return json
            .Replace("{jobId}", jobId.ToString())
            .Replace("{lifeCycleState}", lifeCycleState.ToString())
            .Replace("{resultState}", resultState.ToString());
    }
}
