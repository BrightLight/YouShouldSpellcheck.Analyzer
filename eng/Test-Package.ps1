[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$analyzerProject = Join-Path $repositoryRoot 'source\YouShouldSpellcheck.Analyzer\YouShouldSpellcheck.Analyzer.csproj'
$packageOutput = Join-Path $repositoryRoot 'artifacts\packages'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('YouShouldSpellcheck.PackageTest.' + [Guid]::NewGuid().ToString('N'))

function Invoke-DotNet {
  param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)

  & dotnet @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
  }
}

try {
  New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null
  New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
  $nuspecOutput = Join-Path $testRoot 'pack'
  $intermediateOutput = Join-Path $testRoot 'obj'
  $buildOutput = Join-Path $testRoot 'bin'
  New-Item -ItemType Directory -Path $nuspecOutput -Force | Out-Null

  Invoke-DotNet pack $analyzerProject --configuration $Configuration --no-restore --output $packageOutput `
    "-p:IntermediateOutputPath=$intermediateOutput\" `
    "-p:OutputPath=$buildOutput\" `
    "-p:NuspecOutputPath=$nuspecOutput\" `
    "-p:UseSharedCompilation=false"

  $packageVersion = (& dotnet msbuild $analyzerProject -getProperty:PackageVersion -nologo).Trim()
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($packageVersion)) {
    throw 'Could not determine PackageVersion from the analyzer project.'
  }

  $packagePath = Join-Path $packageOutput "YouShouldSpellcheck.Analyzer.$packageVersion.nupkg"
  if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Expected package was not created at '$packagePath'."
  }

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $packageArchive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
  try {
    $packageEntries = @($packageArchive.Entries | ForEach-Object { $_.FullName })
    $requiredEntries = @(
      'analyzers/dotnet/cs/YouShouldSpellcheck.Analyzer.dll',
      'analyzers/dotnet/cs/YouShouldSpellcheck.Analyzer.CodeFixes.dll',
      'analyzers/dotnet/cs/WeCantSpell.Hunspell.dll',
      'buildTransitive/YouShouldSpellcheck.Analyzer.props',
      'buildTransitive/YouShouldSpellcheck.Analyzer.targets',
      'buildTransitive/dictionaries/en_US.aff',
      'buildTransitive/dictionaries/en_US.dic',
      'buildTransitive/dictionaries/LICENSE_en_US.txt'
    )

    foreach ($requiredEntry in $requiredEntries) {
      if ($requiredEntry -notin $packageEntries) {
        throw "The package does not contain required entry '$requiredEntry'."
      }
    }

    $contentDictionary = $packageEntries | Where-Object {
      $_ -match '^contentFiles/any/any/dictionaries/'
    } | Select-Object -First 1
    if ($contentDictionary) {
      throw "Bundled dictionaries must not be NuGet contentFiles because those become visible consumer project items ('$contentDictionary')."
    }

    $hostAssembly = $packageEntries | Where-Object {
      $_ -match '^analyzers/dotnet/cs/Microsoft\.CodeAnalysis(?:\.|/)' `
        -or $_ -match '^analyzers/dotnet/cs/System\.Composition(?:\.|/)'
    } | Select-Object -First 1
    if ($hostAssembly) {
      throw "The package must not contain host-owned assembly '$hostAssembly'."
    }
  }
  finally {
    $packageArchive.Dispose()
  }

  $consumerProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <WarningsAsErrors>`$(WarningsAsErrors);YS100;YS101;YS103;YS104;YS106</WarningsAsErrors>
    <RestorePackagesPath>`$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
    <YouShouldSpellcheckDefaultLanguages>en-US</YouShouldSpellcheckDefaultLanguages>
    <YouShouldSpellcheckStringLiteralLanguages>none</YouShouldSpellcheckStringLiteralLanguages>
    <YouShouldSpellcheckMaxSuggestionsPerLanguage>3</YouShouldSpellcheckMaxSuggestionsPerLanguage>
    <YouShouldSpellcheckMaxSuggestions>4</YouShouldSpellcheckMaxSuggestions>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YouShouldSpellcheck.Analyzer" Version="$packageVersion" PrivateAssets="all" />
    <YouShouldSpellcheckAttributeArgument
      Include="PackageConsumer.UiTextAttribute"
      Member="Text"
      Languages="en-US;de-DE" />
    <Content Include="CustomDictionary.en_US.txt" />
  </ItemGroup>
</Project>
"@

  $source = @"
namespace PackageConsumer;

[System.AttributeUsage(System.AttributeTargets.Class)]
public sealed class UiTextAttribute : System.Attribute
{
  public string Text { get; set; } = string.Empty;
}

/// <summary>Provides meal preparation.</summary>
[UiText(Text = "mispeling")]
public class TypName
{
  public const string IgnoredText = "ordnary";

  /// <summary>This mehtod prepares the meal.</summary>
  public string PrepateMeal() => string.Empty;
}
"@

  Set-Content -LiteralPath (Join-Path $testRoot 'PackageConsumer.csproj') -Value $consumerProject -Encoding utf8
  Set-Content -LiteralPath (Join-Path $testRoot 'Class1.cs') -Value $source -Encoding utf8

  Invoke-DotNet restore (Join-Path $testRoot 'PackageConsumer.csproj') --source $packageOutput --no-cache

  $evaluatedItems = (& dotnet msbuild (Join-Path $testRoot 'PackageConsumer.csproj') `
    -getItem:AdditionalFiles -getItem:None -getItem:CompilerVisibleProperty `
    -getItem:YouShouldSpellcheckAttributeArgument -nologo | Out-String | ConvertFrom-Json)
  if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the clean consumer project items.'
  }

  $bundledAdditionalFiles = @($evaluatedItems.Items.AdditionalFiles | Where-Object {
    $_.FullPath -match '[\\/]buildTransitive[\\/]dictionaries[\\/].+\.(?:dic|aff)$'
  })
  if ($bundledAdditionalFiles.Count -eq 0) {
    throw 'The bundled dictionaries were not imported as AdditionalFiles.'
  }
  $nonHiddenBundledAdditionalFiles = @($bundledAdditionalFiles | Where-Object {
    $_.Visible -ne 'false'
  })
  if ($nonHiddenBundledAdditionalFiles.Count -ne 0) {
    throw 'Bundled dictionary AdditionalFiles must carry Visible=false for IDE project systems.'
  }

  $attributeArgumentItems = @($evaluatedItems.Items.YouShouldSpellcheckAttributeArgument)
  if ($attributeArgumentItems.Count -ne 1 -or $attributeArgumentItems[0].Visible -ne 'false') {
    throw 'Attribute argument configuration items must carry Visible=false for IDE project systems.'
  }

  $languageToolModeProperty = @($evaluatedItems.Items.CompilerVisibleProperty | Where-Object {
    $_.Identity -eq 'YouShouldSpellcheckLanguageToolMode'
  })
  if ($languageToolModeProperty.Count -ne 1) {
    throw 'The package did not expose YouShouldSpellcheckLanguageToolMode as a compiler-visible property.'
  }

  $attributeArgumentsProperty = @($evaluatedItems.Items.CompilerVisibleProperty | Where-Object {
    $_.Identity -eq 'YouShouldSpellcheckAttributeArgumentsEncoded'
  })
  if ($attributeArgumentsProperty.Count -ne 1) {
    throw 'The package did not expose attribute argument items through a compiler-visible property.'
  }

  foreach ($suggestionPropertyName in @('YouShouldSpellcheckMaxSuggestionsPerLanguage', 'YouShouldSpellcheckMaxSuggestions')) {
    $suggestionProperty = @($evaluatedItems.Items.CompilerVisibleProperty | Where-Object {
      $_.Identity -eq $suggestionPropertyName
    })
    if ($suggestionProperty.Count -ne 1) {
      throw "The package did not expose $suggestionPropertyName as a compiler-visible property."
    }
  }

  $visibleBundledFiles = @($evaluatedItems.Items.None | Where-Object {
    $_.FullPath -match '[\\/]buildTransitive[\\/]dictionaries[\\/]'
  })
  if ($visibleBundledFiles.Count -ne 0) {
    throw 'Bundled dictionaries were also imported as visible None items in the consumer project.'
  }

  $buildOutput = (& dotnet build (Join-Path $testRoot 'PackageConsumer.csproj') --no-restore --verbosity minimal 2>&1 | Out-String)
  if ($LASTEXITCODE -eq 0) {
    throw "The consumer build unexpectedly succeeded; the package analyzer did not raise YS103.`n$buildOutput"
  }

  foreach ($expectedDiagnostic in @('YS100', 'YS103', 'YS104', 'YS106')) {
    if ($buildOutput -notmatch "\berror $expectedDiagnostic\b") {
      throw "The consumer build failed without the expected $expectedDiagnostic diagnostic.`n$buildOutput"
    }
  }

  if ($buildOutput -match '\b(?:error|warning) YS101\b') {
    throw "The 'none' language sentinel did not disable string-literal diagnostics.`n$buildOutput"
  }

  if ($buildOutput -match '\b(CS8032|AD0001)\b') {
    throw "The analyzer failed to load or execute in the package consumer.`n$buildOutput"
  }

  Set-Content -LiteralPath (Join-Path $testRoot 'CustomDictionary.en_US.txt') -Value "Ui`nTyp`nPrepate`nmehtod`nmispeling" -Encoding utf8
  $customDictionaryBuildOutput = (& dotnet build (Join-Path $testRoot 'PackageConsumer.csproj') --no-restore --verbosity minimal 2>&1 | Out-String)
  if ($LASTEXITCODE -ne 0) {
    throw "The consumer build failed after adding 'Typ' through a Content custom dictionary.`n$customDictionaryBuildOutput"
  }

  if ($customDictionaryBuildOutput -match '\b(YS100|YS103|CS8032|AD0001)\b') {
    throw "The Content custom dictionary was not imported as an AdditionalFile.`n$customDictionaryBuildOutput"
  }

  Write-Host "Package smoke test passed: $packagePath"
}
finally {
  if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
  }
}
