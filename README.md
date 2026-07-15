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

## Analyzer execution model

The compiler analyzer is synchronous and deterministic, as required by Roslyn's diagnostic callback API. Settings, Hunspell dictionaries, custom words, parsed dictionaries, and spelling caches are scoped to each compilation. This allows projects with different language configurations to be analyzed concurrently in the same compiler or IDE process.

Configuration, `.dic`/`.aff` pairs, and custom word lists are supplied as MSBuild `AdditionalFiles`. A custom word list is named `CustomDictionary.<language>.txt`; for example:

```xml
<ItemGroup>
  <AdditionalFiles Include="CustomDictionary.en_US.txt" />
</ItemGroup>
```

`LanguageToolUrl` remains readable for configuration compatibility, but the compiler analyzer no longer performs LanguageTool HTTP requests. Remote grammar checking should run as a separate explicitly invoked tool or IDE feature where asynchronous network work has a supported lifetime.

The former “Add to custom dictionary” code action was removed because it changed a file outside the Roslyn workspace while returning an unchanged document. Custom dictionaries can still be edited as normal project files.

The analyzer project uses AppVeyor's `APPVEYOR_BUILD_VERSION` as `PackageVersion`, and `appveyor.yml` sets the build version format to `1.2.{build}` and publishes the result directly as a NuGet-package artifact. This gives every CI package artifact a unique `.nupkg` filename while local packages continue to use the version declared in the analyzer project.
