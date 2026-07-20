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

## MSBuild language configuration

The following MSBuild properties are supported by the package. They are ordinary evaluated MSBuild properties, so they can be set in an individual `.csproj`, in `Directory.Build.props` or `Directory.Build.targets`, or in a `.props`/`.targets` file imported by the project. Use a `.props` file for shared defaults; use a `.targets` file only when the setting deliberately needs to be applied after project evaluation.

All language-selection values use a semicolon-separated list of BCP 47 tags, such as `en-US;de-DE`. Tags are case-insensitive when resolved, but use conventional BCP 47 casing in source-controlled configuration.

| Property | Value | Effect |
| --- | --- | --- |
| `YouShouldSpellcheckDefaultLanguages` | BCP 47 list | Languages used when a category has no more-specific setting. |
| `YouShouldSpellcheckIdentifierLanguages` | BCP 47 list | Languages for identifiers; class, method, variable, property, enum, enum member, and event names inherit this when not configured separately. |
| `YouShouldSpellcheckClassNameLanguages` | BCP 47 list | Languages for class names. |
| `YouShouldSpellcheckMethodNameLanguages` | BCP 47 list | Languages for method names. |
| `YouShouldSpellcheckVariableNameLanguages` | BCP 47 list | Languages for variable names. |
| `YouShouldSpellcheckPropertyNameLanguages` | BCP 47 list | Languages for property names. |
| `YouShouldSpellcheckEnumNameLanguages` | BCP 47 list | Languages for enum type names. |
| `YouShouldSpellcheckEnumMemberNameLanguages` | BCP 47 list | Languages for enum members. |
| `YouShouldSpellcheckEventNameLanguages` | BCP 47 list | Languages for event names. |
| `YouShouldSpellcheckCommentLanguages` | BCP 47 list | Languages for XML documentation text. |
| `YouShouldSpellcheckStringLiteralLanguages` | BCP 47 list | Languages for ordinary string literals. |
| `YouShouldSpellcheckDictionaryMappings` | `BCP-47-tag=Hunspell-file-name` entries, separated by semicolons | Maps a configured tag to the base name of its `.dic`/`.aff` pair. |
| `YouShouldSpellcheckLanguageToolMappings` | `BCP-47-tag=LanguageTool-tag` entries, separated by semicolons | Overrides the LanguageTool code for exceptional tags. Unmapped tags use their BCP 47 tag directly. |
| `YouShouldSpellcheckLanguageToolMode` | `Off`, `AutoFallback`, or `CompilationEnd` | Overrides the XML `LanguageToolMode` setting. |
| `YouShouldSpellcheckLanguageToolUrl` | URL, for example `http://localhost:8081/v2` | Overrides the XML `LanguageToolUrl` setting. |
| `YouShouldSpellcheckLanguageToolScope` | `StringLiteralsAndAttributeArguments`, `AttributeArgumentsOnly`, or `StringLiteralsOnly` | Overrides the XML `LanguageToolScope` setting. |
| `YouShouldSpellcheckLanguageToolTimeoutSeconds` | Positive integer | Overrides the XML LanguageTool request timeout; values below 1 are clamped to 1. |
| `YouShouldSpellcheckLanguageToolMaxConcurrency` | Positive integer | Overrides the XML maximum simultaneous LanguageTool requests; values below 1 are clamped to 1. |

The package provides these bundled dictionary mappings by default: `de-DE=de_DE_frami`, `en-GB=en_GB`, `en-US=en_US`, `nl-NL=nl_NL`, `ru-RU=russian-aot-ieyo`, `sv-FI=sv_FI`, `sv-SE=sv_SE`, and `uk-UA=uk_UA`.

### Per-project configuration

Configure a single project directly in its `.csproj`:

```xml
<PropertyGroup>
  <YouShouldSpellcheckDefaultLanguages>en-US;de-DE</YouShouldSpellcheckDefaultLanguages>
  <YouShouldSpellcheckIdentifierLanguages>en-US</YouShouldSpellcheckIdentifierLanguages>
  <YouShouldSpellcheckStringLiteralLanguages>en-US;de-DE</YouShouldSpellcheckStringLiteralLanguages>
</PropertyGroup>
```

For a project-owned dictionary, map the public tag to that dictionary's file base name and add both dictionary files as `AdditionalFiles`:

```xml
<PropertyGroup>
  <YouShouldSpellcheckDefaultLanguages>en-US;de-AT</YouShouldSpellcheckDefaultLanguages>
  <YouShouldSpellcheckDictionaryMappings>en-US=en_US;de-AT=de_AT_custom</YouShouldSpellcheckDictionaryMappings>
</PropertyGroup>

<ItemGroup>
  <AdditionalFiles Include="dictionaries\de_AT_custom.dic" />
  <AdditionalFiles Include="dictionaries\de_AT_custom.aff" />
</ItemGroup>
```

Setting `YouShouldSpellcheckDictionaryMappings` replaces the package default value rather than extending it, so include every mapping required by that project, as in the example above.

### Shared configuration

Place shared settings in a repository-root `Directory.Build.props` to apply them to all descendant projects:

```xml
<Project>
  <PropertyGroup>
    <YouShouldSpellcheckDefaultLanguages>en-US;de-DE</YouShouldSpellcheckDefaultLanguages>
    <YouShouldSpellcheckCommentLanguages>en-US;de-DE</YouShouldSpellcheckCommentLanguages>
    <YouShouldSpellcheckStringLiteralLanguages>en-US;de-DE</YouShouldSpellcheckStringLiteralLanguages>
  </PropertyGroup>
</Project>
```

An imported configuration file works the same way:

```xml
<Import Project="build\Spellcheck.props" />
```

Projects can override a shared value with their own `<PropertyGroup>`. As with normal MSBuild precedence, place that override after the import when importing a custom `.props` file directly.

### Command-line configuration

Every property in the table can also be supplied for a command-line build with `-p:` (or `/p:`). This is useful for CI or a one-off verification, but it does not configure IDE live analysis because Visual Studio and Rider evaluate the project rather than reusing your command-line arguments.

In PowerShell, quote values containing semicolons:

```powershell
dotnet build MySolution.sln `
  '-p:YouShouldSpellcheckDefaultLanguages=en-US;de-DE' `
  '-p:YouShouldSpellcheckStringLiteralLanguages=en-US;de-DE'
```

For a grammar-specific CI build, require LanguageTool without changing the checked-in project configuration:

```powershell
dotnet build MySolution.sln `
  '-p:YouShouldSpellcheckLanguageToolMode=CompilationEnd'
```

The complete LanguageTool connection configuration can also be supplied for a CI build:

```powershell
dotnet build MySolution.sln `
  '-p:YouShouldSpellcheckLanguageToolUrl=http://localhost:8081/v2' `
  '-p:YouShouldSpellcheckLanguageToolMode=CompilationEnd' `
  '-p:YouShouldSpellcheckLanguageToolScope=StringLiteralsOnly' `
  '-p:YouShouldSpellcheckLanguageToolTimeoutSeconds=15' `
  '-p:YouShouldSpellcheckLanguageToolMaxConcurrency=2'
```

To use a project-owned dictionary for one build, pass both the language list and the complete mapping list:

```powershell
dotnet build MyProject.csproj `
  '-p:YouShouldSpellcheckDefaultLanguages=en-US;de-AT' `
  '-p:YouShouldSpellcheckDictionaryMappings=en-US=en_US;de-AT=de_AT_custom'
```

An explicitly set MSBuild property overrides its equivalent setting in `youshouldspellcheck.config.xml`. The XML file remains the compatibility fallback for unset properties. Attribute-specific language rules and suggestion limits remain XML settings for now.

LanguageTool uses the configured BCP 47 tag by default. Set `YouShouldSpellcheckLanguageToolMappings` only when a configured language tag needs a different LanguageTool code; it follows the same semicolon-separated `configured-tag=LanguageTool-tag` syntax as dictionary mappings.

## Custom dictionaries

Custom dictionaries remain supported as project-specific text files. Create one file per language, using the name `CustomDictionary.<language>.txt`. The language part must match the mapped Hunspell dictionary name, not the public BCP 47 tag; for example, the bundled `en-US=en_US` mapping uses `CustomDictionary.en_US.txt`.

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

## Suggestion limits

To keep the IDE code-fix menu focused, the analyzer preserves Hunspell's suggestion order, removes case-insensitive duplicates, and reports at most five suggestions per configured language and eight suggestions per diagnostic by default. Configure either limit in `youshouldspellcheck.config.xml`; use `0` to remove that limit:

```xml
<SpellcheckSettings>
  <MaxSuggestionsPerLanguage>5</MaxSuggestionsPerLanguage>
  <MaxSuggestions>8</MaxSuggestions>
</SpellcheckSettings>
```

The overall limit is shared by all local dictionaries. LanguageTool has no per-language relevance score in its response, so its distinct replacements use the overall `MaxSuggestions` limit and retain server order.

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

All LanguageTool settings can instead be supplied as MSBuild properties for that project:

```xml
<PropertyGroup>
  <YouShouldSpellcheckLanguageToolUrl>http://localhost:8081/v2</YouShouldSpellcheckLanguageToolUrl>
  <YouShouldSpellcheckLanguageToolMode>AutoFallback</YouShouldSpellcheckLanguageToolMode>
  <YouShouldSpellcheckLanguageToolScope>StringLiteralsAndAttributeArguments</YouShouldSpellcheckLanguageToolScope>
  <YouShouldSpellcheckLanguageToolTimeoutSeconds>30</YouShouldSpellcheckLanguageToolTimeoutSeconds>
  <YouShouldSpellcheckLanguageToolMaxConcurrency>1</YouShouldSpellcheckLanguageToolMaxConcurrency>
</PropertyGroup>
```

The NuGet package defaults this MSBuild override to `Off` during Visual Studio design-time builds when the project has not set it explicitly. This keeps local YS100 and YS101 diagnostics in the editor's live syntax-analysis phase, where replacement and custom-dictionary code fixes are available. Normal builds still use the mode from `youshouldspellcheck.config.xml`, so an XML setting of `AutoFallback` continues to run LanguageTool and fall back to Hunspell for build diagnostics. LanguageTool YS201–YS217 diagnostics and deferred `AutoFallback` diagnostics are compilation-end diagnostics; Visual Studio displays them but does not offer document code fixes for them.

To deliberately run LanguageTool during design-time analysis as well, set `YouShouldSpellcheckLanguageToolMode` explicitly in the project. This also opts into the compilation-end code-fix limitation.

`LanguageToolMaxConcurrency` defaults to 1, bounds simultaneous requests per compilation, and is clamped to at least 1. Increase it only when the configured server can handle parallel projects and concurrent requests. `LanguageToolTimeoutSeconds` is the HTTP timeout and is also clamped to at least 1. The analyzer does not depend on `ThreadHelper`, `JoinableTaskFactory`, RestSharp, or a JSON package; those would either tie it to Visual Studio or add analyzer load-context dependencies without changing Roslyn's synchronous callback contract.

The analyzer project uses AppVeyor's `APPVEYOR_BUILD_VERSION` as `PackageVersion`, and `appveyor.yml` sets the build version format to `1.2.{build}` and publishes the result directly as a NuGet-package artifact. This gives every CI package artifact a unique `.nupkg` filename while local packages continue to use the version declared in the analyzer project.
