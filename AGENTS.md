# Repository guidance

## Project purpose

YouShouldSpellcheck.Analyzer is a Roslyn analyzer and code-fix package for C#. Its distinguishing requirement is project-configurable, simultaneous language support. Different language sets can apply to identifiers, XML documentation, string literals, and selected properties or constructor parameters of selected attributes.

Preserve this multi-language behavior when changing analyzer architecture or configuration.

## Repository layout

- `source/YouShouldSpellcheck.Analyzer`: analyzer, code fixes, configuration, dictionary handling, and NuGet packaging files.
- `source/YouShouldSpellcheck.Analyzer.Tests`: NUnit and Roslyn analyzer/code-fix tests.
- `dictionaries`: bundled Hunspell `.dic`/`.aff` pairs and their license files.
- `demo/SpellcheckerDemo`: legacy consumer example.
- `REVIEW_FINDINGS.md`: modernization findings and proposed work order.

## Build and test

Run commands from the repository root:

```powershell
dotnet restore source\YouShouldSpellcheck.Analyzer.sln
dotnet build source\YouShouldSpellcheck.Analyzer.sln --configuration Release --no-restore
dotnet test source\YouShouldSpellcheck.Analyzer.sln --configuration Release --no-build
```

When changing packaging, also create a package and test it from a clean consumer project. A project-reference test does not verify analyzer dependency loading or package-provided `AdditionalFiles`.

## Analyzer design constraints

- Analyzer state must be scoped to a compilation/project. Do not introduce process-wide mutable state for settings, dictionaries, caches, or availability flags.
- Prefer immutable per-compilation state created from a compilation-start action and captured by registered callbacks.
- Analyzer callbacks must complete synchronously. Do not use `async void`, fire-and-forget work, network calls, or other operations that can outlive a Roslyn analysis context.
- Analyzer execution must be deterministic and suitable for command-line builds, CI, Visual Studio, and restricted build hosts.
- Read configuration, Hunspell dictionaries, and custom word lists through Roslyn `AdditionalFiles`/`AdditionalText` and analyzer configuration options. Do not perform direct filesystem I/O during analysis.
- Report actionable diagnostics for invalid configuration, missing dictionary pairs, and unknown configured languages. Do not silently suppress analyzer failures.
- Resolve attributes and constructor parameters semantically through Roslyn symbols rather than comparing source text.
- Keep compiler (`Microsoft.CodeAnalysis*`) references private and compatible with supported hosts. Package non-Roslyn runtime dependencies beside the analyzer assembly.
- Treat `netstandard2.0` as a compatibility target, not as an obsolete target that must automatically be replaced by the newest .NET target.

## Configuration and language behavior

- A word is accepted when it is valid in any language configured for the relevant source category.
- Category-specific language settings fall back to identifier or default language settings as documented by the configuration model.
- Attribute rules must continue to support distinct languages per attribute property or constructor parameter.
- Configuration and custom dictionaries must be independently selectable per project, including when multiple projects are analyzed in one compiler or IDE process.
- Missing or malformed configuration must have explicit, tested behavior.

## Testing expectations

Add focused regression tests with each behavioral change. Important scenarios include:

- two compilations using different configurations and dictionaries in the same process;
- concurrent analyzer execution;
- configuration and dictionaries supplied as `AdditionalFiles`;
- malformed configuration, missing `.dic`/`.aff` pairs, and unknown languages;
- short, qualified, aliased, and suffixed attribute names plus overloaded constructors;
- regular, verbatim, interpolated, raw, and escaped strings, with exact diagnostic spans;
- XML documentation text and entities;
- clean NuGet consumer builds under supported SDK/MSBuild versions.

Tests must fail on unexpected exceptions. Do not turn arbitrary failures into ignored or inconclusive results.

## Change discipline

- Keep modernization work incremental; use `REVIEW_FINDINGS.md` as the backlog and update it when findings are resolved or superseded.
- Preserve dictionary license and attribution files when changing package contents.
- Avoid unrelated formatting or generated-file churn.
- Document user-visible configuration and packaging changes in the root README.
