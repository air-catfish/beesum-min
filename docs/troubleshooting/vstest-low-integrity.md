# `dotnet test` aborts with `Win32Exception (5)` on Windows 11 build 28000

**Status:** environment issue, not a Beesum bug. This page is a ready-to-paste
report for [microsoft/vstest](https://github.com/microsoft/vstest/issues).

## Summary

`dotnet test` aborts before any test runs, on every .NET project, with:

```text
Unhandled exception. System.ComponentModel.Win32Exception (5): Access is denied.
   at System.Diagnostics.Process.GetProcessHandle(Int32 access, Boolean throwIfExited)
   at System.Diagnostics.Process.GetOrOpenProcessHandle()
   at System.Diagnostics.Process.EnsureWatchingForExit()
   at System.Diagnostics.Process.set_EnableRaisingEvents(Boolean value)
   at Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.ProcessHelper.SetExitCallback(...)
   at Microsoft.VisualStudio.TestPlatform.TestHost.DefaultEngineInvoker.SetParentProcessExitCallback(...)
```

Reproduced with a stock `dotnet new xunit` project, so it is not specific to any
repository.

## Root cause observed

A `DOTNET_STARTUP_HOOKS` probe that logs the integrity SID of the process token
(`GetTokenInformation(TokenIntegrityLevel)` + `ConvertSidToStringSid`) reports:

| Process | Integrity |
| --- | --- |
| `dotnet` (MSBuild) | `S-1-16-8192` (Medium) |
| `vstest.console` (hosted by `dotnet`) | `S-1-16-8192` (Medium) |
| `testhost` | `S-1-16-4096` (Low) |

From inside `testhost`:

```text
OpenProcess(SYNCHRONIZE, parentPid)          -> OK
OpenProcess(PROCESS_ALL_ACCESS, parentPid)   -> ERROR_ACCESS_DENIED (5)
```

`DefaultEngineInvoker.SetParentProcessExitCallback` calls
`Process.EnableRaisingEvents`, and .NET opens the parent process with
`PROCESS_ALL_ACCESS`. A Low-integrity process cannot obtain write access to a
Medium-integrity process, so the call throws and the exception is not caught:
`testhost` dies before test discovery, and the run ends with `TESTRUNABORT`.

The low integrity is applied to `testhost` by the platform, not by the project:
the same happens under a Task Scheduler task and a WMI-spawned process (i.e.
outside any harness sandbox), and `testhost.exe` is a valid Microsoft-signed
binary (`asInvoker` manifest).

## Minimal repro

```powershell
mkdir C:\temp\repro
cd C:\temp\repro
dotnet new xunit
dotnet test
```

## Expected

Tests are discovered and executed.

## Actual

```text
error TESTRUNABORT: Test run aborted.
Unhandled exception. System.ComponentModel.Win32Exception (5): Access is denied.
```

## Environment

- Windows 11 Pro, version 10.0.28000 (build 28000)
- .NET SDK 10.0.403, runtime 10.0.14
- VSTest 18.7.0-release-26523-112
- `Microsoft.NET.Test.Sdk` 17.14.1 and 18.11.0 (both fail)
- `xunit` 2.9.3 and `xunit.v3` 3.2.2 (both fail under `dotnet test`)

## Workarounds

- **xunit v3 / Microsoft.Testing.Platform:** run the test project as an
  executable: `dotnet run --project tests\Beesum.Core.Tests`. This bypasses
  `testhost` and works even while the machine is in this state.
- **xunit v2 / VSTest:** no supported workaround; tests have to be executed
  in-process by a custom runner.
