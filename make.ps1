<#
.SYNOPSIS
    Powershell script to build Riak .NET Client
.DESCRIPTION
    This script ensures that your build environment is sane and will run MSBuild.exe correctly depending on parameters passed to this script.
.PARAMETER Target
    Target to build. Can be one of the following:
        * Debug           - debug build that is not versioned (default)
        * Release         - release build that versioned
        * All             - debug and release build (parallel)
        * Clean           - clean build artifacts
        * Test            - Run all tests
        * UnitTest        - Run unit tests
        * IntegrationTest - Run live integration tests
.PARAMETER Verbosity
    Parameter to set MSBuild verbosity
.EXAMPLE
    C:\Users\Bashoman> cd Projects\basho\riak-dotnet-client
    C:\Users\Bashoman\Projects\basho\riak-dotnet-client>.\make.ps1 Debug
.EXAMPLE
    C:\Users\Bashoman> cd Projects\basho\riak-dotnet-client
    C:\Users\Bashoman\Projects\basho\riak-dotnet-client>.\make.ps1 -Target Release -Verbosity Detailed
.NOTES
    Author: Luke Bakken
    Date:   January 28, 2015
#>
[CmdletBinding()]
Param(
    [Parameter(Mandatory=$False, Position=0)]
    [ValidateSet('Debug','Release', 'All', 'Publish', 'Clean', 'CleanAll',
        'Test','TestAll','UnitTest','IntegrationTest','DeprecatedTest','CodeAnalysis',
        IgnoreCase = $True)]
    [string]$Target = 'Debug',
    [Parameter(Mandatory=$False)]
    [ValidateSet('Quiet','q','Minimal','m','Normal','n',
        'Detailed','d','Diagnostic','diag', IgnoreCase = $True)]
    [string]$Verbosity = 'Normal',
    [Parameter(Mandatory=$False)]
    [ValidatePattern("^v[1-9]\.[0-9]\.[0-9](-[a-z0-9]+)?")]
    [string]$VersionString
)

Set-StrictMode -Version Latest

trap
{
    Write-Error -ErrorRecord $_
    exit 1
}

if ([System.Environment]::Version.Major -ne 4) {
    throw "The build depends on CLR version 4"
}

function Get-ScriptPath {
    $scriptDir = Get-Variable PSScriptRoot -ErrorAction SilentlyContinue | ForEach-Object { $_.Value }
    if (!$scriptDir) {
        if ($MyInvocation.MyCommand.Path) {
            $scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
        }
    }
    if (!$scriptDir) {
        if ($ExecutionContext.SessionState.Module.Path) {
            $scriptDir = Split-Path (Split-Path $ExecutionContext.SessionState.Module.Path)
        }
    }
    if (!$scriptDir) {
        $scriptDir = $PWD
    }
    return $scriptDir
}

function Get-PathToMSBuildExe {
    $msbuild_exe_path = ''
    $msbuild_exe_name = 'MSBuild.exe'

    $clr_version = @([System.Environment]::Version.Major, [System.Environment]::Version.Minor, [System.Environment]::Version.Build)
    $clr_version_str = 'v' + [String]::Join('.', $clr_version)

    $install_root = (Get-ItemProperty -Name InstallRoot HKLM:\SOFTWARE\Microsoft\.NETFramework).InstallRoot
    $msbuild_exe_path = Join-Path -Path $install_root -ChildPath (Join-Path -Path $clr_version_str -ChildPath $msbuild_exe_name)
    if (!(Test-Path $msbuild_exe_path)) {
        $install_root = (Get-ItemProperty -Name InstallRoot HKLM:\SOFTWARE\Wow6432Node\Microsoft\.NETFramework).InstallRoot
        $msbuild_exe_path = Join-Path -Path $install_root -ChildPath (Join-Path -Path $clr_version_str -ChildPath $msbuild_exe_name)
        if (!(Test-Path $msbuild_exe_path)) {
            throw "Could not find $msbuild_exe_name on this system"
        }
    }

    Write-Debug "Using $msbuild_exe_name at $msbuild_exe_path"

    return $msbuild_exe_path
}

function Get-BuildTargetsFile {
    Param([Parameter(Mandatory=$True, Position=0)]
          [string]$ScriptPath)

    $build_targets_file_name = 'build.targets'

    $build_dir = Join-Path -Path $ScriptPath -ChildPath 'build'
    $build_targets_file = Join-Path -Path $build_dir -ChildPath 'build.targets'
    if (!(Test-Path $build_targets_file)) {
        throw "Could not find $build_targets_file_name on this system"
    }

    return $build_targets_file
}

function Get-PathToNuGetExe {
    Param([Parameter(Mandatory=$True, Position=0)]
          [string]$NuGetDir)

    $nuget_exe_name = 'NuGet.exe'

    $nuget_exe_path = Join-Path -Path $NuGetDir -ChildPath $nuget_exe_name

    if (!(Test-Path $nuget_exe_path)) {
        throw "Could not find $nuget_exe_name on this system"
    }

    Write-Debug "Using $nuget_exe_name at $nuget_exe_path"

    return $nuget_exe_path
}

# Note:
# Set to Continue to see DEBUG messages
# $DebugPreference = 'Continue'

Write-Debug "Target: $Target"

$version_property = ''
if (! ([String]::IsNullOrEmpty($VersionString))) {
    if ($Target -ne 'Publish') {
        throw 'Only use the -VersionString parameter for the "Publish" target'
    }
    $version_property = "/property:VersionString=$VersionString"
}

$script_path = Get-ScriptPath

$build_targets_file = Get-BuildTargetsFile -ScriptPath $script_path

$msbuild_exe = Get-PathToMSBuildExe

$nuget_dir = Join-Path -Path $script_path -ChildPath '.nuget'
$nuget_exe = Get-PathToNuGetExe -NuGetDir $nuget_dir
$nuget_packages_dir = Join-Path -Path $script_path -ChildPath 'packages'
$nuget_packages_config = Join-Path -Path $nuget_dir -ChildPath 'packages.config'
Write-Debug "NuGet command: $nuget_exe restore -PackagesDirectory $nuget_packages_dir $nuget_packages_config"
& $nuget_exe restore -PackagesDirectory $nuget_packages_dir $nuget_packages_config
if ($? -ne $True) {
    throw "$nuget_exe failed: $LastExitCode"
}
Write-Debug "$nuget_exe exit code: $LastExitCode"

$verbose_property = ''
if ($Verbosity -eq 'detailed' -or $Verbosity -eq 'd' -or
    $Verbosity -eq 'diagnostic' -or $Verbosity -eq 'diag') {
    $verbose_property = '/property:Verbose=true'
}

# Fix up Target to use CleanAll in build.targets file
if ($Target -eq 'Clean') {
    $Target = 'CleanAll'
}

$maxcpu_property = ''
if ($env:USERNAME -eq 'buildbot')
{
    $env:MSBUILDDISABLENODEREUSE = 1
    $maxcpu_property = '/maxcpucount:1'
}
else
{
    $env:MSBUILDDISABLENODEREUSE = 0
    $maxcpu_property = '/maxcpucount'
}

Write-Debug "MSBuild command: $msbuild_exe ""/verbosity:$Verbosity"" /nologo /m ""/property:SolutionDir=$script_path\"" ""$maxcpu_property"" ""$version_property"" ""$verbose_property"" ""/target:$Target"" ""$build_targets_file"""
& $msbuild_exe "/verbosity:$Verbosity" /nologo /m "/property:SolutionDir=$script_path\" "$maxcpu_property" "$version_property" "$verbose_property" "/target:$Target" "$build_targets_file"
if ($? -ne $True) {
    throw "$msbuild_exe failed: $LastExitCode"
}
Write-Debug "$msbuild_exe exit code: $LastExitCode"
exit 0

