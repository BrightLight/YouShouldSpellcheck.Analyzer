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

The compiler analyzer uses synchronous Roslyn callbacks. Settings, Hunspell dictionaries, custom words, parsed dictionaries, spelling caches, and optional LanguageTool work are scoped to each compilation. This allows projects with different language configurations to be analyzed concurrently in the same compiler or IDE process.

Configuration, `.dic`/`.aff` pairs, and custom word lists are supplied as MSBuild `AdditionalFiles`. A custom word list is named `CustomDictionary.<language>.txt`; for example:

```xml
<ItemGroup>
  <AdditionalFiles Include="CustomDictionary.en_US.txt" />
</ItemGroup>
```

## Optional LanguageTool checks

LanguageTool is disabled by default, so ordinary IDE and build analysis remains offline and deterministic. It can be explicitly enabled for nightly builds or an IDE session with a local LanguageTool server:

```xml
<SpellcheckSettings>
  <StringLiteralLanguages>
    <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
  </StringLiteralLanguages>
  <LanguageToolUrl>http://localhost:8081/v2</LanguageToolUrl>
  <LanguageToolMode>CompilationEnd</LanguageToolMode>
  <LanguageToolTimeoutSeconds>30</LanguageToolTimeoutSeconds>
  <LanguageToolMaxConcurrency>4</LanguageToolMaxConcurrency>
</SpellcheckSettings>
```

In `CompilationEnd` mode, the analyzer collects string literals, configured attribute arguments, and XML documentation text without making requests from syntax callbacks. At the compilation-end callback it sends bounded asynchronous requests, waits for the batch while the Roslyn context is valid, and reports LanguageTool results as YS201–YS217 diagnostics. This preserves normal Roslyn/SonarQube diagnostic output and LanguageTool replacement suggestions. YS218 reports an invalid, unreachable, timed-out, or failing explicitly configured server.

When this mode handles a text, LanguageTool replaces its local Hunspell check. Identifier checks continue to use Hunspell. Network availability and response time necessarily affect enabled builds, so keep the mode off for builds that must remain hermetic. In an IDE, diagnostics arrive at the end of an analysis pass rather than one string at a time.

`LanguageToolMaxConcurrency` bounds simultaneous requests per compilation and is clamped to at least 1. `LanguageToolTimeoutSeconds` is the HTTP timeout and is also clamped to at least 1. The analyzer does not depend on `ThreadHelper`, `JoinableTaskFactory`, RestSharp, or a JSON package; those would either tie it to Visual Studio or add analyzer load-context dependencies without changing Roslyn's synchronous callback contract.

The former “Add to custom dictionary” code action was removed because it changed a file outside the Roslyn workspace while returning an unchanged document. Custom dictionaries can still be edited as normal project files.

The analyzer project uses AppVeyor's `APPVEYOR_BUILD_VERSION` as `PackageVersion`, and `appveyor.yml` sets the build version format to `1.2.{build}` and publishes the result directly as a NuGet-package artifact. This gives every CI package artifact a unique `.nupkg` filename while local packages continue to use the version declared in the analyzer project.
