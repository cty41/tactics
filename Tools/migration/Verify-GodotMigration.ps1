[CmdletBinding()]
param(
    [string]$GodotExecutable = 'D:\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectRoot = (Resolve-Path (Join-Path $repoRoot 'godot')).Path
$projectFile = Join-Path $projectRoot 'project.godot'
$adapterProject = Join-Path $projectRoot 'Tactics.Godot.Adapter.csproj'
$testHostProject = Join-Path $projectRoot 'Tactics.Godot.TestHost.csproj'
$solution = Join-Path $repoRoot 'Tactics.Migration.slnx'
$runSettings = Join-Path $repoRoot 'Tactics.Migration.runsettings'
$poisonExport = Join-Path $repoRoot 'Tools\migration\out\poison-spear-lv1.unity.json'
$poisonDraft = Join-Path $repoRoot 'Tools\migration\out\poison-spear-lv1.draft.json'
$poisonSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\poison-spear-lv1.json'
$poisonExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\poison-spear-lv1-export.json'
$poisonGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\poison-spear-lv1-real.json'
$poisonGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\poison-spear-lv1-generation.json'
$unitExport = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1.unity.json'
$unitDraft = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1.draft.json'
$unitGolden = Join-Path $repoRoot 'Tests\golden\unit-batch-v1.json'
$unitSpecification = Join-Path $repoRoot 'Tools\migration\manifest\export-batches\pure-run-units-v1.json'
$unitExportReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-units-v1-export.json'
$unitGenerationLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-units-v1.json'
$unitTextureLedger = Join-Path $repoRoot 'Tools\migration\manifest\state\pure-run-unit-textures-v1.json'
$unitGenerationReceipt = Join-Path $repoRoot 'Tools\migration\manifest\receipts\pure-run-units-v1-generation.json'
$unitGalleryCapture = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1-gallery.png'
$unitSpawnCapture = Join-Path $repoRoot 'Tools\migration\out\pure-run-units-v1-spawn.png'
$unitGoatTintShader = Join-Path $projectRoot 'src\Tactics.Godot.Adapter\Runtime\Shaders\GoatBodyTint.gdshader'
$systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$releaseVerificationDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $systemTempRoot ("tactics-godot-release-" + [Guid]::NewGuid().ToString('N'))))
$requiredTempPrefix = $systemTempRoot + [System.IO.Path]::DirectorySeparatorChar
if (-not $releaseVerificationDirectory.StartsWith($requiredTempPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Release verification directory escaped the system temp root: $releaseVerificationDirectory"
}

foreach ($requiredFile in @($GodotExecutable, $projectFile, $adapterProject, $testHostProject, $solution, $runSettings)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required migration file not found: $requiredFile"
    }
}

$trackedProjects = @(git -C $repoRoot ls-files -- 'project.godot' '*/project.godot')
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to enumerate tracked Godot projects.'
}
if ($trackedProjects.Count -ne 1 -or $trackedProjects[0] -ne 'godot/project.godot') {
    throw "Expected exactly one tracked Godot project at godot/project.godot; found: $($trackedProjects -join ', ')"
}

function Invoke-Checked {
    param(
        [string]$Description,
        [scriptblock]$Command
    )

    Write-Host "== $Description =="
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repoRoot
try {
    $env:GODOT_BIN = $GodotExecutable

    $codexProjectConfig = Join-Path $repoRoot '.codex\config.toml'
    if (Test-Path -LiteralPath $codexProjectConfig -PathType Leaf) {
        Invoke-Checked 'Validate project-scoped godot-ai Codex configuration' {
            powershell -NoProfile -ExecutionPolicy Bypass -File 'Tools/migration/Sync-GodotAiCodexConfig.ps1' -Check
        }
    }
    else {
        Write-Host '== Skip project-scoped godot-ai Codex configuration: local config is not present =='
    }

    Invoke-Checked 'Restore locked migration dependencies' {
        dotnet restore $solution --locked-mode
    }

    # Build/test steps are intentionally sequential. Separate processes have
    # previously contended for Tactics.Core/obj and produced intermittent locks.
    Invoke-Checked 'Build migration solution (single MSBuild node)' {
        dotnet build $solution -c Debug --no-restore -m:1
    }

    Invoke-Checked 'Run Tactics.Core NUnit' {
        dotnet test 'src/Tactics.Core.Tests/Tactics.Core.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Run Tactics.Application NUnit' {
        dotnet test 'src/Tactics.Application.Tests/Tactics.Application.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Run frozen Unity source Oracle NUnit' {
        dotnet test 'src/Tactics.UnityOracle.Tests/Tactics.UnityOracle.Tests.csproj' -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Run agent policy unittest' {
        python -m unittest discover -s 'Tools/agent-policy' -p 'test_*.py'
    }

    Invoke-Checked 'Validate Godot Incidents' {
        python 'Tools/agent-policy/validate_godot_incidents.py'
    }

    foreach ($skill in @(
        'godot-workflow',
        'godot-csharp-development',
        'godot-content-migration',
        'godot-editor-tooling',
        'godot-editor-lifecycle',
        'godot-testing-diagnostics',
        'godot-ai-workflow')) {
        Invoke-Checked "Validate Codex/OpenCode skill: $skill" {
            powershell -NoProfile -ExecutionPolicy Bypass -File '.agents/scripts/validate-skills.ps1' -SkillsRoot ".agents/skills/$skill"
        }
    }

    Invoke-Checked 'Restore isolated GdUnit4Net test host dependencies' {
        dotnet restore $testHostProject --locked-mode
    }

    Invoke-Checked 'Build isolated GdUnit4Net test host non-incrementally' {
        dotnet build $testHostProject -c Debug --no-restore --no-incremental -m:1
    }

    Invoke-Checked 'Run isolated GdUnit4Net test host' {
        dotnet test $testHostProject -c Debug --no-restore --no-build --settings $runSettings --logger 'console;verbosity=minimal'
    }

    Invoke-Checked 'Restore production Godot Debug assembly after GdUnit' {
        dotnet build $adapterProject -c Debug --no-restore --no-incremental -m:1
    }

    if (Test-Path -LiteralPath $poisonExport -PathType Leaf) {
        Invoke-Checked 'Compile real Poison Spear typed migration draft' {
            python -m Tools.migration.poison_spear_converter `
                --export $poisonExport `
                --specification $poisonSpecification `
                --output $poisonDraft
        }

        $generatedTargets = @(
            'godot/content/poison_spear/PoisonBuff.tres',
            'godot/content/poison_spear/PoisonSpearSkillLv1.tres',
            'godot/content/poison_spear/PoisonSpearPresentationLv1.tres',
            'godot/content/poison_spear/PoisonSpear10x10Fixture.tres',
            'godot/content/poison_spear/PoisonSpearProjectile.tscn',
            'godot/content/poison_spear/PoisonSpearImpact.tscn',
            'godot/content/poison_spear/ContentCatalog.tres',
            'Tools/migration/manifest/state/poison-spear-lv1-real.json'
        )
        Invoke-Checked 'Generate real Poison Spear Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/PoisonSpearAssetBuilder.cs'
        }
        $firstGenerationHashes = @{}
        foreach ($target in $generatedTargets) {
            $firstGenerationHashes[$target] = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat real Poison Spear generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/PoisonSpearAssetBuilder.cs'
        }
        foreach ($target in $generatedTargets) {
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
            if ($firstGenerationHashes[$target] -ne $secondHash) {
                throw "Poison Spear generation is not byte-idempotent: $target"
            }
        }

    }
    else {
        Write-Host '== Skip real Poison Spear regeneration: disposable Unity DTO is not present =='
    }

    if (Test-Path -LiteralPath $unitExport -PathType Leaf) {
        Invoke-Checked 'Compile real Pure Run Unit typed migration draft' {
            python -m Tools.migration.unit_converter `
                --export $unitExport `
                --specification $unitSpecification `
                --golden $unitGolden `
                --output $unitDraft
        }

        Invoke-Checked 'Copy approved project-owned Unit PNG payload transactionally' {
            python -m Tools.migration.unit_texture_migration --root $repoRoot --draft $unitDraft
        }
        $firstTextureHashes = @{}
        foreach ($artifact in (Get-Content -LiteralPath $unitTextureLedger -Raw | ConvertFrom-Json).artifacts) {
            $firstTextureHashes[$artifact.relativePath] = (
                Get-FileHash -LiteralPath (Join-Path $repoRoot $artifact.relativePath) -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Unit PNG migration for idempotency' {
            python -m Tools.migration.unit_texture_migration --root $repoRoot --draft $unitDraft
        }
        foreach ($target in $firstTextureHashes.Keys) {
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
            if ($firstTextureHashes[$target] -ne $secondHash) {
                throw "Pure Run Unit PNG migration is not byte-idempotent: $target"
            }
        }

        # ResourceSaver must resolve imported Texture2D resources on a clean checkout.
        Invoke-Checked 'Import Pure Run Unit PNG payload in headless Editor' {
            & $GodotExecutable --headless --editor --path $projectRoot --quit-after 6000
        }

        $unitGeneratedTargets = @(
            'godot/content/units/ContentCatalog.tres',
            'godot/content/units/PureRunAmazon.tres',
            'godot/content/units/PureRunFireDemon.tres',
            'godot/content/units/PureRunGoatAoe.tres',
            'godot/content/units/PureRunGoatCharger.tres',
            'godot/content/units/PureRunGoatEliteCharger.tres',
            'godot/content/units/PureRunGoatElitePoisonCaster.tres',
            'godot/content/units/PureRunGoatRanged.tres',
            'godot/content/units/PureRunGoatSupport.tres',
            'godot/content/units/PureRunMage.tres',
            'godot/content/units/PureRunNecromancer.tres',
            'godot/content/units/PureRunSkeletonMage.tres',
            'godot/content/units/PureRunSkeletonWarrior.tres',
            'godot/content/units/UnitActor.tscn',
            'godot/content/units/UnitGallery.tscn',
            'godot/content/units/UnitSpawnFixture.tscn',
            'Tools/migration/manifest/state/pure-run-units-v1.json'
        )
        Invoke-Checked 'Generate Pure Run Unit Godot assets through ResourceSaver' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/UnitAssetBuilder.cs'
        }
        $firstUnitGenerationHashes = @{}
        foreach ($target in $unitGeneratedTargets) {
            $firstUnitGenerationHashes[$target] = (
                Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
        }
        Invoke-Checked 'Repeat Pure Run Unit generation for idempotency' {
            & $GodotExecutable --headless --path $projectRoot `
                --script 'res://src/Tactics.Godot.Adapter/Editor/UnitAssetBuilder.cs'
        }
        foreach ($target in $unitGeneratedTargets) {
            $secondHash = (Get-FileHash -LiteralPath (Join-Path $repoRoot $target) -Algorithm SHA256).Hash
            if ($firstUnitGenerationHashes[$target] -ne $secondHash) {
                throw "Pure Run Unit generation is not byte-idempotent: $target"
            }
        }
    }
    else {
        Write-Host '== Skip real Pure Run Unit regeneration: disposable Unity DTO is not present =='
    }

    # A ResourceSaver script can register a newly created UID only in its current process.
    # The headless Editor filesystem scan persists the project UID cache before Runtime validation.
    Invoke-Checked 'Godot editor filesystem scan and plugin initialization' {
        & $GodotExecutable --headless --editor --path $projectRoot --quit-after 6000
    }

    Invoke-Checked 'Build Release without test sources or dev packages' {
        New-Item -ItemType Directory -Path $releaseVerificationDirectory -ErrorAction Stop | Out-Null
        dotnet build $adapterProject -c Release --no-restore -m:1 --output $releaseVerificationDirectory
    }

    $forbiddenReleaseAssemblies = @(
        Get-ChildItem -LiteralPath $releaseVerificationDirectory -File -ErrorAction Stop |
            Where-Object { $_.Name -match '^(GdUnit|Microsoft\.TestPlatform|testhost)' }
    )
    if ($forbiddenReleaseAssemblies.Count -gt 0) {
        throw "Release contains test assemblies: $($forbiddenReleaseAssemblies.Name -join ', ')"
    }
    $releaseDependencyFile = Join-Path $releaseVerificationDirectory 'Tactics.Godot.Adapter.deps.json'
    $releaseDependencies = Get-Content -LiteralPath $releaseDependencyFile -Raw -ErrorAction Stop
    if ($releaseDependencies -match 'gdUnit4|Microsoft\.TestPlatform|testhost') {
        throw 'Release dependency manifest contains GdUnit or TestPlatform entries.'
    }

    Invoke-Checked 'Poison Spear catalog and Core validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Poison Spear catalog and Core validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Poison Spear Tween and Scope validation' {
        & $GodotExecutable --headless --path $projectRoot --play-poison-spear --quit-after 6000
    }

    Invoke-Checked 'Pure Run Unit catalog, factory, and fixture validation (Compatibility)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            --validate-units --quit-after 6000
    }

    Invoke-Checked 'Pure Run Unit catalog, factory, and fixture validation (Forward+)' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method forward_plus `
            --validate-units --quit-after 6000
    }

    Invoke-Checked 'Capture deterministic Pure Run Unit programmatic gallery' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            -- --capture-unit-gallery
    }

    Invoke-Checked 'Capture deterministic Pure Run Unit 10x10 spawn fixture' {
        & $GodotExecutable --headless --path $projectRoot --rendering-method gl_compatibility `
            -- --capture-unit-spawn
    }

    if (Test-Path -LiteralPath $poisonExport -PathType Leaf) {
        # Only write a "passed" generation receipt after UID scan, both renderer paths,
        # runtime semantics, Tween, and scope validation have actually succeeded.
        Invoke-Checked 'Refresh real Poison Spear generation receipt' {
            python -m Tools.migration.poison_spear_receipt `
                --export-receipt $poisonExportReceipt `
                --draft $poisonDraft `
                --ledger $poisonGenerationLedger `
                --output $poisonGenerationReceipt
        }

        Invoke-Checked 'Validate regenerated Poison Spear ledger and receipt' {
            python -m unittest Tools.migration.tests.test_poison_spear_generation
        }
    }

    if (Test-Path -LiteralPath $unitExport -PathType Leaf) {
        # Visual acceptance remains manual even after the deterministic gallery is captured.
        Invoke-Checked 'Refresh Pure Run Unit generation receipt' {
            python -m Tools.migration.unit_generation_receipt `
                --export-receipt $unitExportReceipt `
                --draft $unitDraft `
                --generation-ledger $unitGenerationLedger `
                --texture-ledger $unitTextureLedger `
                --gallery-capture $unitGalleryCapture `
                --spawn-capture $unitSpawnCapture `
                --goat-tint-shader $unitGoatTintShader `
                --output $unitGenerationReceipt
        }

        Invoke-Checked 'Validate regenerated Pure Run Unit ledger and receipt' {
            python -m unittest Tools.migration.tests.test_unit_generation
        }
    }

    Invoke-Checked 'Run migration Python unittest against refreshed generation evidence' {
        python -m unittest discover -s 'Tools/migration/tests' -p 'test_*.py'
    }

    Invoke-Checked 'Run OKF unittest' {
        python -m unittest discover -s (Join-Path $repoRoot 'Tools/okf') -p 'test_*.py'
    }

    Invoke-Checked 'Validate OKF bundle' {
        python 'Tools/okf/validate_bundle.py'
    }

    Invoke-Checked 'Report OKF worktree impact' {
        python 'Tools/okf/catalog_impact.py' report --worktree
    }

    Invoke-Checked 'Validate patch whitespace' {
        git diff --check
    }

    Write-Host "Godot migration verification passed. Canonical project: $projectRoot"
}
finally {
    if (Test-Path -LiteralPath $releaseVerificationDirectory -PathType Container) {
        [System.IO.Directory]::Delete($releaseVerificationDirectory, $true)
    }
    Pop-Location
}
