<#
.SYNOPSIS
Find affected hosts given a set of affected .NET project files

.OUTPUTS
Hostnames that are affected
#>

function Find-AffectedHosts {
    param (
        $AffectedProjects,
        $Hostnames
    )

    $hostnamesOutput = ($hostNames | ConvertFrom-Json)

    foreach ($hostName in ($Hostnames | ConvertFrom-Json)) {
        foreach ($project in $hostname.PSObject.Properties) {
            $hostnamesOutput.$($project.Name) = ''

            foreach ($csprojFile in $project.Value) {
                foreach ($affectedProjectFilePath in ($affectedProjects | ConvertFrom-Json).Filepath) {
                    if ($affectedProjectFilePath.Contains($csprojFile)) {

                        # No less than 4 foreach loops, I know...
                        Write-Host "$($project.Name) is affected by a change in $affectedProjectFilePath"
                        $hostnamesOutput.$($project.Name) = 'true'
                    }
                }            
            }
        }
    }
    
    return $hostnamesOutput | ConvertTo-Json -Compress
}

<#
.SYNOPSIS

Install and run dotnet-affected
#>
function Write-AffectedProjectsFile {
    param (
        $SolutionPath,
        $WorkspacePath,
        $FromSha,
        $ToSha
    )

    dotnet tool install dotnet-affected --global
    dotnet affected --solution-path $solutionPath -p $workspacePath --from $fromSha --to $toSha --format traversal json
}