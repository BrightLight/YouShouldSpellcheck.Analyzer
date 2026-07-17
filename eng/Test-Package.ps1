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
    <WarningsAsErrors>`$(WarningsAsErrors);YS103</WarningsAsErrors>
    <RestorePackagesPath>`$(MSBuildProjectDirectory)\.packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YouShouldSpellcheck.Analyzer" Version="$packageVersion" PrivateAssets="all" />
    <AdditionalFiles Include="youshouldspellcheck.config.xml" />
    <Content Include="CustomDictionary.en_US.txt" />
  </ItemGroup>
</Project>
"@

  $settingsXml = @"
<?xml version="1.0" encoding="utf-8"?>
<SpellcheckSettings>
  <DefaultLanguages>
    <Language LocalDictionaryLanguage="en_US" LanguageToolLanguage="en-US" />
  </DefaultLanguages>
</SpellcheckSettings>
"@

  $source = @"
namespace PackageConsumer;

public class TypName
{
}
"@

  Set-Content -LiteralPath (Join-Path $testRoot 'PackageConsumer.csproj') -Value $consumerProject -Encoding utf8
  Set-Content -LiteralPath (Join-Path $testRoot 'youshouldspellcheck.config.xml') -Value $settingsXml -Encoding utf8
  Set-Content -LiteralPath (Join-Path $testRoot 'Class1.cs') -Value $source -Encoding utf8

  Invoke-DotNet restore (Join-Path $testRoot 'PackageConsumer.csproj') --source $packageOutput --no-cache

  $evaluatedItems = (& dotnet msbuild (Join-Path $testRoot 'PackageConsumer.csproj') `
    -getItem:AdditionalFiles -getItem:None -getItem:CompilerVisibleProperty -nologo | Out-String | ConvertFrom-Json)
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

  $languageToolModeProperty = @($evaluatedItems.Items.CompilerVisibleProperty | Where-Object {
    $_.Identity -eq 'YouShouldSpellcheckLanguageToolMode'
  })
  if ($languageToolModeProperty.Count -ne 1) {
    throw 'The package did not expose YouShouldSpellcheckLanguageToolMode as a compiler-visible property.'
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

  if ($buildOutput -notmatch '\berror YS103\b') {
    throw "The consumer build failed without the expected YS103 diagnostic.`n$buildOutput"
  }

  if ($buildOutput -match '\b(CS8032|AD0001)\b') {
    throw "The analyzer failed to load or execute in the package consumer.`n$buildOutput"
  }

  Set-Content -LiteralPath (Join-Path $testRoot 'CustomDictionary.en_US.txt') -Value 'Typ' -Encoding utf8
  $customDictionaryBuildOutput = (& dotnet build (Join-Path $testRoot 'PackageConsumer.csproj') --no-restore --verbosity minimal 2>&1 | Out-String)
  if ($LASTEXITCODE -ne 0) {
    throw "The consumer build failed after adding 'Typ' through a Content custom dictionary.`n$customDictionaryBuildOutput"
  }

  if ($customDictionaryBuildOutput -match '\b(YS103|CS8032|AD0001)\b') {
    throw "The Content custom dictionary was not imported as an AdditionalFile.`n$customDictionaryBuildOutput"
  }

  Write-Host "Package smoke test passed: $packagePath"
}
finally {
  if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
  }
}
