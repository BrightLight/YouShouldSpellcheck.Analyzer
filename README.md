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

Configuration, `.dic`/`.aff` pairs, and custom word lists are supplied as MSBuild `AdditionalFiles`.
Bundled dictionary files remain hidden package assets and are not added as visible content items to consuming projects.

## Custom dictionaries

Custom dictionaries remain supported as project-specific text files. Create one file per language, using the name `CustomDictionary.<language>.txt`. The language part must match the configured `LocalDictionaryLanguage`; for example, use `CustomDictionary.en_US.txt` for `en_US`.

List one accepted word per line:

```text
OpenAI
Silverseed
Zorbax
```

Custom words are case-sensitive. Custom dictionaries in the project root are discovered automatically by the analyzer package, including files that Visual Studio or Rider persist using the ordinary `Content` item type. No manual project-file change is required for dictionaries created by the code fix.

If a custom dictionary is stored in a subdirectory or outside the project directory, add it to the consuming project explicitly as an MSBuild `AdditionalFile`:

```xml
<ItemGroup>
  <AdditionalFiles Include="CustomDictionary.en_US.txt" />
</ItemGroup>
```

Words can be managed directly in the text file or through the spelling diagnostic's code fixes. For each configured language, the analyzer offers an action to add the reported word to an existing custom dictionary. If the project does not yet contain a custom dictionary for that language, it instead offers to create `CustomDictionary.<language>.txt` and add the word in one step.

These actions create or update a Roslyn `AdditionalDocument`, so the edit is represented as a solution change instead of an untracked filesystem side effect. This allows supporting IDE hosts to preview, apply, and undo the change through their normal project-system integration. An IDE may display a newly created `.txt` file with a `Content` build action; the package's imported build targets also pass matching project-root files to Roslyn as `AdditionalFiles` during evaluation.

The legacy `<CustomDictionariesFolder>` configuration element is retained for configuration compatibility but is no longer used during analysis. Custom dictionary files must be supplied through `AdditionalFiles` as shown above.

## Optional LanguageTool checks

LanguageTool is disabled by default, so ordinary IDE and build analysis remains offline and deterministic. It can be explicitly enabled for nightly builds or an IDE session with a local LanguageTool server:

```xml
<SpellcheckSettings>
  <StringLiteralLanguages>
    <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
  </StringLiteralLanguages>
  <LanguageToolUrl>http://localhost:8081/v2</LanguageToolUrl>
  <LanguageToolMode>AutoFallback</LanguageToolMode>
  <LanguageToolScope>StringLiteralsAndAttributeArguments</LanguageToolScope>
  <LanguageToolTimeoutSeconds>30</LanguageToolTimeoutSeconds>
  <LanguageToolMaxConcurrency>1</LanguageToolMaxConcurrency>
</SpellcheckSettings>
```

LanguageTool has three execution modes:

- `Off` always uses local dictionaries and never accesses the network.
- `AutoFallback` probes LanguageTool with one real text. When the server is available and every request succeeds, it reports LanguageTool results as YS201–YS217. If the probe or any later request fails, it discards all LanguageTool results and reports local Hunspell diagnostics for the entire compilation instead. An unavailable optional server does not produce YS218.
- `CompilationEnd` requires LanguageTool. It retains successful candidate results when another request fails and reports YS218 for invalid, unreachable, timed-out, or failing requests.

Both enabled modes collect complete string literals and configured attribute arguments without making requests from syntax callbacks. XML documentation and identifiers continue to use the local dictionaries. Requests run as a deduplicated, bounded batch at compilation end while the Roslyn context remains valid.

`LanguageToolScope` defaults to `StringLiteralsAndAttributeArguments`. Set it to `AttributeArgumentsOnly` to reproduce the narrower legacy LanguageTool scope, or to `StringLiteralsOnly` to exclude configured attribute arguments.

When LanguageTool handles a text, it replaces its local Hunspell check. If multiple LanguageTool languages apply, a source span is reported only when every configured language flags that span. Network availability and response time necessarily affect enabled builds. In an IDE, diagnostics arrive at the end of an analysis pass rather than one string at a time.

`AutoFallback` supports a checked-in URL while developers and ordinary CI agents remain independent of LanguageTool. A grammar-specific build can require the server without modifying the checked-in XML:

```xml
<PropertyGroup>
  <YouShouldSpellcheckLanguageToolMode>CompilationEnd</YouShouldSpellcheckLanguageToolMode>
</PropertyGroup>
```

The MSBuild property accepts the same `Off`, `AutoFallback`, and `CompilationEnd` values and overrides `<LanguageToolMode>` for that project.

The NuGet package defaults this MSBuild override to `Off` during Visual Studio design-time builds when the project has not set it explicitly. This keeps local YS100 and YS101 diagnostics in the editor's live syntax-analysis phase, where replacement and custom-dictionary code fixes are available. Normal builds still use the mode from `youshouldspellcheck.config.xml`, so an XML setting of `AutoFallback` continues to run LanguageTool and fall back to Hunspell for build diagnostics. LanguageTool YS201–YS217 diagnostics and deferred `AutoFallback` diagnostics are compilation-end diagnostics; Visual Studio displays them but does not offer document code fixes for them.

To deliberately run LanguageTool during design-time analysis as well, set `YouShouldSpellcheckLanguageToolMode` explicitly in the project. This also opts into the compilation-end code-fix limitation.

`LanguageToolMaxConcurrency` defaults to 1, bounds simultaneous requests per compilation, and is clamped to at least 1. Increase it only when the configured server can handle parallel projects and concurrent requests. `LanguageToolTimeoutSeconds` is the HTTP timeout and is also clamped to at least 1. The analyzer does not depend on `ThreadHelper`, `JoinableTaskFactory`, RestSharp, or a JSON package; those would either tie it to Visual Studio or add analyzer load-context dependencies without changing Roslyn's synchronous callback contract.

The analyzer project uses AppVeyor's `APPVEYOR_BUILD_VERSION` as `PackageVersion`, and `appveyor.yml` sets the build version format to `1.2.{build}` and publishes the result directly as a NuGet-package artifact. This gives every CI package artifact a unique `.nupkg` filename while local packages continue to use the version declared in the analyzer project.
