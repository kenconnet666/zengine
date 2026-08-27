# ZEngine New-Machine Handoff

This document is the operational continuation guide for moving ZEngine to another computer. The architecture blueprint explains *why* the system is shaped this way; this document explains *exactly what must move, what is reproducible, and how to prove the receiving machine is ready*.

## 1. Authoritative state

The authoritative source state is the current Git `HEAD`, not `bin/`, `obj/`, `.idea/`, `artifacts/`, a running Rider window or a prior screenshot.

At the start of handoff preparation on 2026-08-27:

- Branch: `master`.
- Remote: `https://github.com/kenconnet666/zengine.git`.
- Local and live `origin/master` both resolved to `aa9b839b0bb2a8ba720e3d66ab9fe5eadbcbae5e`.
- P0-P7 were complete at that commit with 67/67 Release tests and 67/67 Debug validation-layer tests.
- The handoff documentation and scripts are a later local commit. Unless that commit is explicitly pushed after this document is written, cloning the remote alone will not contain it. The generated `handoff-manifest.json` is the authority for the final exported HEAD and ahead/behind counts.

Always check after receiving the repository:

```powershell
git rev-parse HEAD
git status -sb
git log --oneline --decorate -10
```

## 2. What must and must not move

| Item | In Git | Action on the new computer |
|---|---:|---|
| Source, tests, shaders, generated SPIR-V, pinned `vk.xml`, docs | Yes | Clone remote or restore the Git bundle |
| All 30+ local commits and branches | Bundle/remote | Verify against `handoff-manifest.json` |
| `.NET 11 Preview 7` SDK | No | Install exact SDK `11.0.100-preview.7.26381.103` |
| NuGet packages | No | Recreated by `dotnet restore` from nuget.org |
| DXC 1.9.2607 portable tools | No | Run `eng/tools/install-dxc.ps1` |
| Vulkan SDK 1.4.357 validation subset | No | Run `eng/tools/install-vulkan-validation.ps1` |
| Vulkan loader and GPU driver | No | Install an appropriate current driver on the receiving OS |
| Rider installation and `.idea/` | No | Install Rider with .NET 11 preview support; reopen `ZEngine.slnx` |
| Codex/Rider MCP configuration | No | Recreate with the new absolute project path and Rider MCP port |
| Validation screenshots | Optional evidence | Export with `-IncludeEvidence` if desired |
| Self-contained game publish | Optional binary | Rebuild, or export with `-IncludePublishedGame` |
| Credentials, GitHub tokens, Context7 keys, Codex login | Never | Authenticate independently; do not copy secrets into the repo or bundle |

`artifacts/`, `bin/`, `obj/`, `.idea/` and `TestResults/` are intentionally ignored. Their absence after cloning is correct.

## 3. Recommended transfer: verified Git bundle

The bundle is safer than copying the live working directory because it contains Git objects and refs without machine-local caches, credentials or IDE state.

On the old computer, after committing all intended handoff changes:

```powershell
cd C:\Users\lionheart\RiderProjects\zengine

.\eng\handoff\export-handoff.ps1 -IncludeEvidence
```

To also carry the approximately 84 MiB self-contained Win-x64 game build:

```powershell
.\eng\handoff\export-handoff.ps1 `
  -IncludeEvidence `
  -IncludePublishedGame
```

Copy the generated `artifacts/handoff/zengine-<timestamp>-<head>/` directory as one unit. It contains:

- `zengine-<head>.bundle` — complete current branch and tags.
- `handoff-manifest.json` — final HEAD, branch, remote, SDK and machine snapshot.
- `SHA256SUMS.txt` — hashes of every payload file.
- Optional `evidence/` screenshots.
- Optional published-game ZIP.

On the new computer, first verify the copied files. The following command checks each line of `SHA256SUMS.txt`:

```powershell
$handoff = 'D:\Transfer\zengine-handoff'
Push-Location $handoff
Get-Content .\SHA256SUMS.txt | ForEach-Object {
    $expected, $relative = $_ -split ' \*', 2
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $relative).Hash
    if ($actual -ne $expected) {
        throw "SHA-256 mismatch: $relative"
    }
}
Pop-Location
```

Then restore:

```powershell
git bundle verify D:\Transfer\zengine-handoff\zengine-<head>.bundle
git clone D:\Transfer\zengine-handoff\zengine-<head>.bundle C:\Code\zengine
cd C:\Code\zengine

git remote set-url origin https://github.com/kenconnet666/zengine.git
git rev-parse HEAD
git status -sb
```

The cloned HEAD must exactly equal `head` in `handoff-manifest.json`.

## 4. Alternative transfer: remote repository

The remote route is simpler only after confirming the final handoff commit has been pushed:

```powershell
git ls-remote https://github.com/kenconnet666/zengine.git refs/heads/master
```

Compare that hash with the manifest. If it matches:

```powershell
git clone https://github.com/kenconnet666/zengine.git C:\Code\zengine
cd C:\Code\zengine
```

If it does not match, use the bundle. Do not silently continue from the older remote state.

## 5. Receiving-machine bootstrap

Required baseline:

- Windows 11 x64 for the currently proven native path.
- Git.
- PowerShell 7.
- Exact .NET SDK `11.0.100-preview.7.26381.103`.
- Radeon/NVIDIA/Intel driver with a conformant Vulkan loader; the proven source machine used Radeon RX 9070 GRE, driver `32.0.31041.1004`.
- Rider version capable of opening .NET 11 Preview projects. CLI build/test remains the acceptance authority.

Run the quick integrity and restore pass:

```powershell
cd C:\Code\zengine
.\eng\handoff\bootstrap-new-machine.ps1
```

Build and run the Release tests:

```powershell
.\eng\handoff\bootstrap-new-machine.ps1 -Build -Test
```

Install pinned portable tools and run the complete visible Vulkan acceptance gate:

```powershell
.\eng\handoff\bootstrap-new-machine.ps1 -FullValidation
```

`-FullValidation` downloads hash-pinned DXC and Vulkan validation assets, then runs Release build/tests/format and the Debug validation-layer window tests. It opens native windows and takes roughly several minutes because it includes the 1,000-reload collectible-ALC stress test.

Expected acceptance:

- Release build: zero warnings, zero errors.
- Release tests: 67 passed, zero failed, zero skipped.
- Formatting gate passes.
- SPIR-V validation passes.
- Native Host resize/minimize/restore and valid/invalid shader reload pass.
- Native UiLab and Game Slice report zero Vulkan validation errors.

## 6. Rider and Codex MCP recreation

Open `ZEngine.slnx` directly in Rider. Do not copy `.idea/` as an authority; let Rider regenerate machine-local state.

The source computer used Rider MCP over streamable HTTP. On the new computer, enable Rider's MCP server, note its new port, and add the equivalent Codex entry with the *new* absolute path:

```toml
[mcp_servers.rider]
url = "http://127.0.0.1:<RIDER_MCP_PORT>/stream"
http_headers = { IJ_MCP_SERVER_PROJECT_PATH = "C:/Code/zengine" }
```

Restart Codex after changing MCP configuration, then make a small read-only project query. `codex mcp list` proving registration is not enough; the query must return project-aware data.

Do not transfer Codex authentication files, GitHub tokens, Context7 keys or other secrets in the handoff directory.

## 7. First commands after bootstrap

Headless engine and dependent-plugin smoke:

```powershell
dotnet run `
  --project samples/ZGame.Slice/ZGame.Slice.csproj `
  -c Release `
  -- `
  --headless `
  --frames=1000 `
  --reload
```

Expected shape:

```text
Gameplay reload: Applied; closure=game.gameplay,game.render,game.director
Headless slice: frames=1000, entities=50000, passes=5, ...
Plugins=game.gameplay,game.render,game.director; residentAssets=3.
```

Visible native game:

```powershell
dotnet run --project samples/ZGame.Slice/ZGame.Slice.csproj -c Release -- --seconds=3
```

Visible editor:

```powershell
dotnet run --project samples/EditorLab.Native/EditorLab.Native.csproj -c Release -- --seconds=3
```

Performance numbers will differ by CPU, GPU, driver, power plan and validation-layer state. Functional invariants and zero validation errors matter more than reproducing the old machine's exact FPS.

## 8. Where to resume development

Read in this order:

1. `README.md` — repository entry and commands.
2. `docs/handoff/README.md` — receiving-machine state and integrity rules.
3. `docs/architecture/zengine-unified-csharp-ui-blazor-vulkan-blueprint.md` — accepted architecture.
4. `docs/development/p0-native-core-status.md` through `p7-game-slice-status.md` — implemented evidence and deliberate boundaries.
5. `samples/ZGame.Slice/` — first integrated consumer of ECS, plugins, assets, RenderGraph and UI.

Recommended next engineering sequence:

1. **GPU glyph-atlas batching.** Replace per-raster-run UI quads with an atlas texture, batched geometry and a bounded draw count while keeping Node/Component/Theme APIs unchanged.
2. **DevHost orchestration.** Add filesystem watch/debounce for shader, asset, style and plugin generations; Runtime itself must remain free of hidden source watchers.
3. **v0.1 API review.** Review contracts only after measuring the atlas path and real DevHost reload loop; do not freeze merely because P0-P7 pass.
4. **Second-platform proof.** Add a Linux adapter and validation environment before claiming cross-platform native support. Android and Apple folders remain allocation points until tested on real toolchains and devices.

The most important known performance boundary is documented in `p7-game-slice-status.md`: native text is correct and supports Chinese system fonts, but the current correctness-first raster-run renderer can emit tens of thousands of quads for editor text. That is the next rendering gate, not a reason to redesign the public C# UI DSL.

## 9. Handoff completion checklist

- [ ] All intended source changes are committed.
- [ ] `git status --short` is empty.
- [ ] Bundle export succeeds and `git bundle verify` passes.
- [ ] `SHA256SUMS.txt` verifies after copying.
- [ ] New clone HEAD matches `handoff-manifest.json`.
- [ ] Exact .NET Preview SDK resolves through `global.json`.
- [ ] `bootstrap-new-machine.ps1 -Build -Test` passes.
- [ ] Full Vulkan validation passes before native renderer work resumes.
- [ ] Rider opens `ZEngine.slnx` without copying old `.idea/` authority.
- [ ] Rider MCP is recreated with the new project path and proven by a project-aware read.
- [ ] No credentials were copied with the repository.
