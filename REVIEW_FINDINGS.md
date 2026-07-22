# Analyzer modernization review

Review date: 2026-07-15  
Environment: .NET SDK 10.0.301, MSBuild 18.6.4

## Verification baseline

After package restore, the Release solution build succeeded and all 11 NUnit tests passed. The build emitted Roslyn analyzer-authoring warnings, including missing extended analyzer rules and diagnostic release tracking.

At review time this verified ordinary project-reference compilation, but not the produced NuGet package in a clean consumer project or the external SonarQube build job. Package-consumer verification has since been added as recorded below; the external job remains outstanding.

## Implementation progress

### 2026-07-22: large-solution analyzer performance

- [x] Limited `AdditionalText.GetText` calls to dictionary pairs and custom word lists referenced by the effective project configuration; unrelated bundled dictionaries remain available without being loaded by the analyzer.
- [x] Cached Hunspell suggestions by language and misspelled word within each compilation, avoiding repeated expensive searches for duplicate misspellings without sharing state between projects.
- [x] Passed Roslyn cancellation through Hunspell checks and suggestion searches so superseded editor analysis stops promptly.
- [x] Added regression coverage proving that unused configured mappings do not cause their dictionary files to be read.

### 2026-07-21: XML configuration removal

- [x] Removed `youshouldspellcheck.config.xml` discovery and deserialization from analyzer execution.
- [x] Removed XML serialization annotations and migrated all tests to compiler-visible MSBuild options.
- [x] Converted the demo and LanguageTool documentation to MSBuild properties and attribute items.
- [x] Deleted the demo XML configuration file; dictionaries and custom words remain tracked `AdditionalFiles`.

### 2026-07-21: remaining scalar configuration

- [x] Added compiler-visible `YouShouldSpellcheckMaxSuggestionsPerLanguage` and `YouShouldSpellcheckMaxSuggestions` properties.
- [x] Added `none` as an explicit empty-set sentinel for language-selection properties while preserving empty-property inheritance.
- [x] Reported YS219 when `none` is combined with language tags.
- [x] Removed the obsolete, unused `CustomDictionariesFolder` setting and its path-resolution code.

### 2026-07-21: MSBuild attribute argument configuration

- [x] Added repeatable `YouShouldSpellcheckAttributeArgument` MSBuild items with attribute type, member, kind, and per-rule language metadata.
- [x] Projected the evaluated item collection through a compiler-visible, analyzer-config-safe property.
- [x] Matched configured attribute types through the bound type symbol and mapped positional arguments through the compiler-selected constructor.
- [x] Distinguished named members from constructor parameters with an optional rule kind and reported malformed item records as YS219.
- [x] Added coverage for aliases, overloaded constructors, rule kind filtering, malformed configuration, and a clean package consumer.

### 2026-07-20: MSBuild language selection

- [x] Added compiler-visible MSBuild properties for default and category-specific language selection, using BCP 47 tags such as `en-US` and `de-DE`.
- [x] Added package-provided mappings from BCP 47 tags to bundled Hunspell file names, including variants such as `de-DE=de_DE_frami`.
- [x] Initially kept XML configuration as a compatibility fallback, then removed it after every supported setting received an MSBuild representation.
- [x] Made LanguageTool use the configured BCP 47 tag by default, with a separate mapping property for exceptional LanguageTool codes.
- [x] Added MSBuild overrides for the LanguageTool URL, mode, scope, timeout, and maximum concurrency.
- [x] Treated empty compiler-visible category properties as unset and safely encoded semicolon-separated MSBuild values so identifiers and XML documentation continue to inherit mapped default languages in package consumers.

### 2026-07-17: LanguageTool request and failure behavior

- [x] Limited LanguageTool candidates to complete string literals and configured attribute arguments; XML documentation remains on the local dictionaries.
- [x] Added `LanguageToolScope` with combined, attribute-only, and string-only modes.
- [x] Deduplicated candidates and reduced default per-compilation concurrency from 4 to 1.
- [x] Made request failures candidate-scoped so successful candidates remain reportable; YS218 summarizes failed requests.
- [x] Applied multi-language acceptance semantics by reporting only spans flagged by every configured LanguageTool language.
- [x] Added request-body, scope, XML exclusion, mixed-success, and multi-language integration coverage.
- [x] Added `AutoFallback`, which probes once and selects either complete LanguageTool results or complete local Hunspell results for the compilation without reporting YS218 for expected unavailability.
- [x] Added the compiler-visible `YouShouldSpellcheckLanguageToolMode` property so grammar-specific builds can require LanguageTool while shared configuration remains in automatic fallback mode.
- [x] Preserved LanguageTool replacement code fixes when an IDE recreates a build diagnostic without its custom properties by recovering suggestions from the analyzer-owned diagnostic message format.
- [x] Separated ordinary text and compilation-end LanguageTool fixes into uniquely named MEF providers.
- [x] Defaulted design-time builds to local checking so Visual Studio can offer document code fixes, while normal builds retain the configured `AutoFallback` or required LanguageTool behavior.

### 2026-07-15: compilation-scoped execution model

- [x] Replaced process-wide analyzer settings, dictionary registrations, parsed dictionaries, custom words, and spelling caches with state created and captured by compilation-start actions.
- [x] Made custom word lists tracked `AdditionalFiles` named `CustomDictionary.<language>.txt`.
- [x] Replaced per-syntax-callback LanguageTool calls with explicit opt-in, compilation-scoped batching at compilation end; removed the RestSharp/JSON runtime dependency closure.
- [x] Replaced the direct-filesystem custom-dictionary code action with workspace-aware actions that create or update tracked `AdditionalDocuments`.
- [x] Removed the fixed-path debug logger and environment-variable expansion from analyzer configuration.
- [x] Made analyzer diagnostics carry Hunspell suggestions so code fixes do not depend on process-wide dictionary state.
- [x] Removed the global test setup and added concurrent two-compilation isolation coverage.
- [x] Enabled Roslyn extended analyzer-authoring rules.

### 2026-07-15: package stabilization

- [x] Replaced the hand-maintained nuspec with SDK-style packing.
- [x] Marked Roslyn and analyzer implementation packages as private package dependencies.
- [x] Placed the analyzer's private runtime dependency closure beside the analyzer assembly.
- [x] Kept host-owned Roslyn and Workspaces assemblies out of the package.
- [x] Packaged bundled dictionaries and license files under `buildTransitive/dictionaries` and imported only `.dic`/`.aff` files through `buildTransitive` props. Keeping them out of NuGet `contentFiles` prevents SDK-style projects from displaying linked dictionary files as ordinary `None` items.
- [x] Imported project-root `CustomDictionary.*.txt` files through `buildTransitive` targets so IDE-created `Content` items still reach the analyzer as `AdditionalFiles`.
- [x] Added `eng/Test-Package.ps1`, which packs, restores into a clean temporary consumer using only the local package source, and verifies an expected `YS103` diagnostic with no analyzer load or execution failure.
- [x] Updated AppVeyor and the analyzer project to apply its unique `1.2.{build}` version to both explicit SDK-style packing and AppVeyor's legacy automatic packaging path, and publish the resulting `.nupkg` without ZIP wrapping.
- [x] Updated the vulnerable transitive `System.Text.Json` 8.0.4 dependency to patched version 8.0.5.
- [ ] Re-run the original SonarQube build job with the repaired package and retain its logs. The external failure has not yet been reproduced in this repository.

## Prioritized findings

### 1. Critical: settings, dictionaries, and caches are process-wide

Status: addressed locally on 2026-07-15.

Evidence:

- `AnalyzerContext.cs` stores the selected settings in a static field and initializes it only when that field is null.
- `DictionaryManager.cs` stores registered dictionary sources, parsed dictionaries, custom words, and spelling results in static collections.
- `AnalyzerContext.RegisterDictionaries` exits as soon as any dictionary has been registered.

Impact:

- The first analyzed project can determine settings and dictionaries for later projects in the same compiler or Visual Studio process.
- Projects cannot reliably use independent language configuration.
- Concurrent project analysis can race during initialization.
- Cache entries do not identify the compilation, project, dictionary version, or custom word list.

Recommended direction:

Create immutable per-compilation analyzer state in a compilation-start action. Parse the configuration and dictionary inputs once for that compilation, then capture the state in syntax or symbol callbacks. Scope spelling caches to that state.

Acceptance checks:

- Two compilations with different language settings produce independent results in the same test process.
- Updating one project's configuration does not change another project's analysis.
- Concurrent analysis produces deterministic results.

### 2. Critical: attribute analysis escapes through `async void`

Status: addressed locally on 2026-07-15. Analyzer callbacks remain synchronous. LanguageTool work is collected during syntax analysis and, only in explicit `CompilationEnd` mode, completed as a bounded batch before the compilation-end callback returns.

Evidence:

- `StringLiteralSpellcheckAnalyzer.AnalyzeStringLiteralToken` calls `AnalyzeAttributeArgument` without awaiting it.
- `AnalyzeAttributeArgument` is `async void` and reports diagnostics after awaiting an HTTP request.

Impact:

- Roslyn can consider the syntax callback complete before the request finishes.
- Diagnostics can be lost or reported after the analysis context is valid.
- Exceptions and cancellation are not handled through the analyzer driver.
- Build results depend on timing and external service availability.

Recommended direction:

Keep syntax callbacks synchronous and free of network access. The implemented compatibility mode collects sentence-like candidates and performs bounded asynchronous HTTP work from a compilation-end action, then synchronously joins that batch while the Roslyn context remains valid. It is off by default; a separately invoked tool remains the preferred future option for fully asynchronous and hermetic CI orchestration.

Acceptance checks:

- No analyzer callback uses `async void` or fire-and-forget work.
- Default analyzer results do not require network availability; explicitly enabled LanguageTool mode reports YS218 when the service fails.
- All analyzer operations observe Roslyn cancellation where applicable.

### 3. Critical: the NuGet package did not include analyzer runtime dependencies

Status: addressed locally on 2026-07-15; verification in the original SonarQube job remains outstanding.

Original evidence:

- The build output contains `WeCantSpell.Hunspell.dll` and `RestSharp.dll`.
- `YouShouldSpellcheck.Analyzer.nuspec` places only `YouShouldSpellcheck.Analyzer.dll` in `analyzers/dotnet/cs`.
- The nuspec declares WeCantSpell.Hunspell as a package dependency but does not declare RestSharp.
- No clean package-consumer test existed.

Impact:

Roslyn analyzer load contexts generally need private runtime dependencies next to the analyzer assembly. A normal NuGet dependency available to the consumer project does not reliably make it available to the analyzer loader. Loader changes in a newer MSBuild/Roslyn version could expose this existing packaging defect and are a plausible cause of the external build failure.

Implemented direction:

The package now uses SDK-style packing, places non-Roslyn runtime dependencies beside the analyzer, marks compiler references private, and excludes host-owned Roslyn and MEF assemblies. Separating IDE-only code fixes remains a possible later simplification.

Acceptance checks:

- [x] A clean project can consume only the generated `.nupkg` and execute the analyzer from command-line MSBuild.
- [ ] The package works with the oldest and newest supported Roslyn/MSBuild hosts.
- [x] Package contents are asserted by an automated test.

### 4. High: analysis performs direct filesystem I/O

Status: addressed for analyzer execution locally on 2026-07-15. Custom word lists are `AdditionalFiles`; custom-dictionary code actions return tracked Roslyn solution changes, and the fixed-path logger was removed.

Evidence:

- `DictionaryManager.GetInMemoryCustomDictionary` uses `File.Exists` and `File.Open` while words are analyzed.
- `Logger` writes debug output to the fixed path `C:\temp\YouShouldSpellcheck.log`.
- The add-to-dictionary code fix directly rewrites a file and returns an unchanged Roslyn document.

Impact:

- Analysis may fail or become nondeterministic in restricted and remote build hosts.
- Inputs are not tracked by MSBuild or represented in the compilation.
- The code-fix side effect is not represented in workspace preview, undo, or Fix All behavior.

Recommended direction:

Supply custom word lists as `AdditionalFiles`, just like Hunspell dictionary pairs. Decide separately how an IDE command should update a custom dictionary file in a workspace-aware way; do not hide the mutation inside an unchanged-document code action.

Acceptance checks:

- Analyzer execution performs no direct filesystem reads or writes.
- Changing a custom word-list `AdditionalFile` invalidates the appropriate analysis.
- Command-line builds work in a read-only source checkout, aside from normal build outputs.

### 5. High: invalid configuration and analyzer failures are silent

Evidence:

- XML deserialization catches all exceptions and returns no settings.
- Analyzer callbacks commonly catch all exceptions, write to debug logging or the console, and return.
- `SpellcheckSettingsWrapper.Attributes` calls `.Select` even though `SpellcheckSettings.Attributes` is nullable.
- A configured language without a registered dictionary causes every checked word to be considered incorrect.

Impact:

Configuration errors can silently disable part or all of the analyzer, while a missing dictionary can instead create a large number of false positives. CI users receive no actionable explanation.

Recommended direction:

Define analyzer diagnostics for malformed configuration, duplicate settings files, unmatched dictionary pairs, unknown languages, and other validation errors. Use explicit safe fallback behavior. Avoid broad exception suppression around normal analyzer callbacks.

Acceptance checks:

- Each invalid-input condition produces one stable, actionable diagnostic.
- Optional collections such as `Attributes` safely default to empty.
- Missing dictionaries do not cause a diagnostic flood.

### 6. High: attribute matching does not use resolved symbols

Status: addressed locally on 2026-07-21. Attribute rules are supplied as structured MSBuild items, and matching uses the compiler-selected attribute constructor and containing type symbol.

Evidence:

- Attribute names are compared using `AttributeSyntax.Name.ToFullString()`.
- Positional arguments are associated with the first constructor having enough parameters instead of the constructor selected by semantic binding.

Impact:

Configured rules can fail for qualified names, aliases, source trivia, and overloads. Positional arguments can receive the language of the wrong constructor parameter. This affects one of the analyzer's central features.

Recommended direction:

Use `SemanticModel.GetSymbolInfo` to identify the resolved attribute constructor and parameter. Normalize configuration around fully qualified metadata names, with an intentional and tested short-name convenience if desired.

Acceptance checks:

- Equivalent short, `Attribute`-suffixed, qualified, and aliased names match consistently.
- Overloaded constructors map every positional argument to the correct parameter.
- Named attribute properties and constructor parameters can be configured unambiguously.

### 7. Medium: every bundled dictionary is added to every consumer compilation

Status: addressed for analyzer loading on 2026-07-22. Bundled pairs remain `AdditionalFiles` so arbitrary per-project mappings work without direct filesystem access, but the analyzer calls `GetText` only for dictionary names selected by effective category or attribute settings. The remaining MSBuild/project-system cost of listing the hidden files is smaller than loading their approximately 20 MB of text per compilation and can be revisited separately.

Evidence:

- `YouShouldSpellcheck.Analyzer.props` adds every packaged `.dic` and `.aff` file as an `AdditionalFile`.
- `CompilationSpellcheckState` filters those files against all effective language settings before reading `SourceText`, then lazily parses only dictionaries used by analyzed words.

Impact:

Every consumer pays evaluation and memory costs for languages it does not use. Large dictionary files can noticeably affect compiler and IDE memory.

Recommended direction:

Keep bundled dictionaries available in the package but allow projects to select the required languages through MSBuild properties/items. Load and parse only dictionary pairs referenced by the effective configuration. Continue to allow project-owned dictionary pairs.

Acceptance checks:

- A project using one language does not load unrelated dictionaries.
- Package and project dictionaries use the same validation and loading pipeline.
- Dictionary licenses and attribution remain in the package.

### 8. Medium: source-location mapping is manually reconstructed

Evidence:

- String diagnostics start by trimming one character from either side of a literal.
- Escaped-character locations are adjusted by counting selected characters in `ValueText`.
- Comments acknowledge missing support for Unicode escapes and incomplete handling of verbatim strings.

Impact:

Diagnostic spans may be wrong for raw strings, interpolated strings, Unicode escapes, doubled quotes, multiline strings, and XML entities. Newer C# syntax increases the number of affected cases.

Recommended direction:

Introduce a token-text-to-value-text mapping abstraction with focused tests for each supported literal form. Prefer Roslyn-provided spans and syntax structure wherever possible.

Acceptance checks:

- Tests assert exact spans for regular, verbatim, interpolated, raw, multiline, and escaped strings.
- XML documentation tests include entities and multiline text.

### 9. Medium: tests mask project isolation and packaging failures

Status: partially addressed locally on 2026-07-15. Tests now receive explicit settings and dictionaries, and concurrent compilation isolation has regression coverage. Broader syntax and invalid-input coverage remains outstanding.

Evidence:

- `SetupTestEnvironment` installs one static analyzer configuration for the entire test run.
- Most tests reference the analyzer project directly rather than consuming its package.
- One string-location test catches every exception and calls `Assert.Inconclusive`.
- There are only 11 tests for analyzers, code fixes, configuration, dictionary handling, packaging, and multiple syntax forms.

Impact:

The suite can pass while package loading, project isolation, configuration parsing, concurrent execution, or modern C# syntax is broken.

Recommended direction:

Remove reliance on global test setup. Give each analyzer test explicit configuration and `AdditionalFiles`. Add package-consumer and multi-compilation regression tests. Unexpected exceptions must fail tests.

Acceptance checks:

- Tests are independent and can run in any order or concurrently.
- A clean package-consumer smoke test runs in CI.
- Configuration parsing and error diagnostics have direct coverage.

### 10. Low: analyzer project conventions and maintenance metadata are incomplete

Evidence:

- `EnforceExtendedAnalyzerRules` is present but commented out.
- The build reports RS1036, RS2008, and RS1016 warnings.
- Diagnostic release tracking files are absent.
- Legacy install/uninstall PowerShell scripts, binding redirects, and Visual Studio 2015 instructions remain.

Recommended direction:

Enable extended analyzer rules, add release tracking, explicitly define Fix All behavior, and remove obsolete package/install artifacts after the replacement packaging flow is verified. Update the README with current installation and configuration instructions.

## Proposed implementation order

1. Capture the failing external build logs and add a clean NuGet consumer smoke test.
2. Repair and modernize package construction and analyzer dependency isolation.
3. Introduce immutable per-compilation settings, dictionaries, and caches.
4. Move custom dictionaries and all remaining analyzer inputs to `AdditionalFiles`.
5. Add configuration validation diagnostics and remove broad exception suppression.
6. Replace textual attribute matching with semantic symbol matching.
7. Remove build-time LanguageTool networking or move it to a separate explicit tool.
8. Improve source-span mapping for modern C# and XML documentation syntax.
9. Expand concurrency, isolation, code-fix, and package integration coverage.
10. Enable analyzer authoring rules, release tracking, and documentation updates.

## Decisions to make during implementation

- [x] All analyzer configuration now uses MSBuild properties and structured attribute-rule items.
- Whether package dictionaries are opt-in by language or a smaller default set is included automatically.
- Custom dictionary updates remain an IDE feature and are represented as `AdditionalDocument` solution changes for project-system persistence, preview, and undo.
- Whether LanguageTool support belongs in a separate command-line/CI tool, an IDE-only component, or is removed.
- Which Roslyn/MSBuild host versions and operating systems the package officially supports.
