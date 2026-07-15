# YouShouldSpellcheck.Analyzer
A Roslyn based spellchecker analyzer

  * supports multiple languages simultaneously
  * fine-grained control over which properties of which attributes are spellchecked (incl. valid language(s) per such property)

[![Build status](https://ci.appveyor.com/api/projects/status/qut9yrj0h4oj9sef?svg=true)](https://ci.appveyor.com/project/MarkusHastreiter/youshouldspellcheck-analyzer)

## Build and package verification

```powershell
dotnet restore source\YouShouldSpellcheck.Analyzer.sln
dotnet test source\YouShouldSpellcheck.Analyzer.sln --configuration Release --no-restore
eng\Test-Package.ps1
```

The package smoke test creates the NuGet package, restores it into a clean temporary consumer project, and verifies that the packaged analyzer and dictionaries produce an expected diagnostic.

The analyzer project uses AppVeyor's `APPVEYOR_BUILD_VERSION` as `PackageVersion`, and `appveyor.yml` sets the build version format to `1.2.{build}`. This gives every CI package artifact a unique version while local packages continue to use the version declared in the analyzer project.
