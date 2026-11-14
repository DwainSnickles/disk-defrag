# Disk Defrag

[![Build](https://github.com/DwainSnickles/disk-defrag/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/DwainSnickles/disk-defrag/actions/workflows/build.yml) [![Release](https://img.shields.io/github/v/release/DwainSnickles/disk-defrag?label=download)](https://github.com/DwainSnickles/disk-defrag/releases/latest)

**Disk Defrag** is a portable Windows disk-optimization front end with a live NTFS cluster map. It calls Windows’ built-in `defrag.exe` for analysis, defragmentation, and media-appropriate optimization, and shows the operation output and progress Windows reports.

- **Analyze** — asks Windows for fragmentation statistics, including fragmented-file count.
- **Defragment** — runs the Windows defragmentation operation.
- **Optimize** — runs Windows’ media-aware optimization for the selected volume.
- **Live cluster map** — reads the NTFS allocation bitmap and refreshes it during an operation.
- **Progress** — shows Windows’ reported phase and percentage inside a raised, custom progress bar. If Windows provides no percentage, the bar explicitly stays indeterminate rather than inventing one.
- **Fragment count** — retains a per-volume analysis result, shows a before/after count, and rechecks after an operation. File count and fragmentation percentage are displayed as separate measurements.
- **Keep awake until done** — optional; asks Windows to keep the system and display awake for the operation.
- **Shut down after completed** — optional; schedules shutdown 60 seconds after a successful Defragment or Optimize operation. It does not apply to Analyze or failed operations. Cancel with `shutdown /a` from an administrator command prompt.
- **Administrator required** — the app requests elevation at startup because Windows requires administrator rights for volume-map access and storage operations.

## Download

Download the self-contained Windows x64 zip from [GitHub Releases](https://github.com/DwainSnickles/disk-defrag/releases/latest). Extract and run `DiskDefrag.exe`; no separate .NET runtime installation is required.

Each release carries three files:

- `Disk-Defrag-win-x64.zip` — the portable build.
- `Disk-Defrag-win-x64.zip.sha256` — checksum for the zip.
- `DiskDefrag.<version>.nupkg` — the same portable build packaged for NuGet, under `tools/`.

## NuGet package

The package distributes the portable build; it is not a library to reference from another project. It carries whatever is in `staging/diskdefrag`, so publish that first — packing without it fails rather than shipping an empty package:

```powershell
dotnet publish DiskDefrag.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o staging/diskdefrag
dotnet pack DiskDefrag.csproj -c Release -o artifacts
```

Download `DiskDefrag.<version>.nupkg` from Releases, or install it from a feed you publish it to:

```powershell
nuget install DiskDefrag -Source <feed>
```

## Build

Requires the .NET 10 SDK and Windows desktop targeting support.

```powershell
dotnet build DiskDefrag.slnx -c Release
```

To publish a self-contained x64 build:

```powershell
dotnet publish DiskDefrag.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o staging/diskdefrag
```

## Safety and scope

Disk Defrag uses Windows’ storage engine rather than implementing raw file moves. Fragment-reduction progress is an estimate based on before/after analysis, not a count of files Windows says it has moved. The tool does not format or partition volumes.

## SEO / repository topics

`disk-defrag` · `windows-defragmenter` · `disk-optimization` · `cluster-map` · `ntfs` · `windows-11` · `windows-10` · `portable` · `defraggler-alternative` · `aml-disk-defrag-alternative` · `defragmentation` · `csharp` · `winforms` · `dotnet10`

## Part of PC Medic

Disk Defrag is the companion project used by [PC Medic](https://github.com/DwainSnickles/pc-medic).
