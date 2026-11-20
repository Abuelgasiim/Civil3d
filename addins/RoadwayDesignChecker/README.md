# Roadway Design Checker — Civil 3D 2025 / 2026 Plugin (.NET 8)

A native **.NET 8** Autodesk Civil 3D plugin that audits roadway **Alignments** and **Profiles** against the **Saudi Highway Code (`SHC 301 – Highway Geometric Design`)**, **AASHTO Green Book (2018, Metric)**, Autodesk Civil 3D **Design Criteria XML** files, and custom **JSON rule overrides** — presented in a human-readable dockable WPF palette with one-click **Show on Drawing** zoom.

---

## Features & Engineering Scope

1. **Horizontal Alignment (`HorizontalAlignmentChecker`)**
   - Minimum Curve Radius ($R_{\min}$) for $e_{\max} \in \{4\%, 6\%, 8\%, 10\%, 12\%\}$ (`SHC 301 Table 4-14 / Eq. 4-6`)
   - Minimum Circular Arc Length (`SHC 301 Table 4-14 / Table 5-2`)
   - Spiral Transition requirement & Minimum/Recommended Spiral Length ($L_s$) (`SHC 301 Table 4-15 & 4-16`)
   - Broken-Back same-direction tangent separation (`400 m` Freeways/Expressways per `SHC 301 Sec. 7.5.3.1`) and maximum straight tangent length (`1,500 m` Rural / `2,000 m` Freeway)
   - Superelevation peak rate ($e \le e_{\max}$) and minimum runoff transition length (`SHC 301 Eq. 4-11 & Table 4-20`)

2. **Vertical Profile (`VerticalProfileChecker`)**
   - Maximum Desirable & Maximum Allowable Grades (`SHC 301 Table 4-17 / 5-5 / 7-4`)
   - Minimum Drainage Grade (`0.5%` min / `0.7%` desirable per `SHC 301 Sec. 4.6.4.2`)
   - Crest & Sag Vertical Curve $K$-values (`SHC 301 Table 4-18 / Eq. 4-7 & 4-9`) — covers Symmetric Parabola, Asymmetric Parabola, and Circular vertical curves
   - Minimum Vertical Curve Length ($0.6 \times V$)

3. **Driver Sight Distance (`SightDistanceChecker`)**
   - Horizontal Stopping Sight Distance lateral clearance middle ordinate $m$ (`SHC 301 Eq. 4-2 & 4-3`)
   - Vertical Crest & Sag Stopping Sight Distance with longitudinal downgrade adjustment (`SHC 301 Eq. 4-1`)
   - Optional two-lane Passing Sight Distance (`SHC 301 Table 4-12 Method 1 & Method 2`)

4. **Human-Readable Civil 3D Experience**
   - Dockable WPF `PaletteSet` (`CHECKROADWAY`, `CHECKROADWAYQUICK`, `ROADWAYSTANDARDS`, `ROADWAYEXPORT`) and **Analyze → Roadway QA** Ribbon panel
   - Plain-English audit cards (`Needs Fix` / `Advisory` / `Passed`) comparing **Your Drawing Has** vs. **Standard Requires** vs. **Result**
   - **Why This Matters** and **How to Fix It in Civil 3D** guidance drawer + engineer waiver notes
   - One-click **Show on Drawing** (`Zoom-to-Station`) and export to **HTML Review Sheet**, **CSV**, and **JSON**

---

## Project Structure

```text
addins/RoadwayDesignChecker/
├── RoadwayDesignChecker.sln
├── Directory.Build.props
├── src/
│   ├── RoadwayDesignChecker.Core/         # Pure .NET 8 engineering engine & SHC 301 / AASHTO JSON tables
│   └── RoadwayDesignChecker.Civil3D/      # Civil 3D 2025/2026 API adapter, Ribbon, Commands & WPF PaletteSet
└── tests/
    └── RoadwayDesignChecker.Core.Tests/   # xUnit tests for formulas, SHC 301 tables, XML parser & audit engine
```

---

## Build & Link with Civil 3D 2025 / 2026 (Windows)

### Prerequisites
- Windows 10/11 (x64)
- **.NET 8 SDK** (`dotnet --version` >= `8.0`)
- **Autodesk Civil 3D 2025** or **Civil 3D 2026**

### Quick Install (One Command — Recommended)
Close Civil 3D, open **PowerShell** inside `addins\RoadwayDesignChecker`, and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-civil3d-plugin.ps1
```

This auto-detects Civil 3D 2025 or 2026, runs the SHC 301 unit tests, compiles the `.NET 8` plugin, and installs `RoadwayDesignChecker.bundle` into `%APPDATA%\Autodesk\ApplicationPlugins\` (no Administrator prompt needed).

---

### Manual Build & Install

### 1. Build the Plugin & Run Unit Tests
From PowerShell inside `addins/RoadwayDesignChecker`:

```powershell
# Run pure engineering unit tests
dotnet test tests/RoadwayDesignChecker.Core.Tests/RoadwayDesignChecker.Core.Tests.csproj

# Build for Civil 3D 2025 (default)
dotnet build src/RoadwayDesignChecker.Civil3D/RoadwayDesignChecker.Civil3D.csproj -c Release -p:Platform=x64

# Or build for Civil 3D 2026
dotnet build src/RoadwayDesignChecker.Civil3D/RoadwayDesignChecker.Civil3D.csproj -c Release -p:Platform=x64 -p:Civil3dYear=2026
```

Building automatically copies `RoadwayDesignChecker.Civil3D.dll`, `RoadwayDesignChecker.Core.dll`, and `Standards/*.json` into `plugins/RoadwayDesignChecker.bundle/Contents/`.

### 2. Option A — Auto-Load in Civil 3D (Recommended)
Copy the entire `plugins/RoadwayDesignChecker.bundle` folder into Autodesk's ApplicationPlugins directory:

```text
%ProgramData%\Autodesk\ApplicationPlugins\RoadwayDesignChecker.bundle
```

Restart Civil 3D 2025 or 2026. The **Roadway QA** panel will appear on the **Analyze** Ribbon tab, and `CHECKROADWAY` will be available immediately on the command line.

### 3. Option B — Manual Load via `NETLOAD` (For Testing)
1. Open Civil 3D 2025 or 2026.
2. Type `NETLOAD` and press Enter.
3. Browse to `addins\RoadwayDesignChecker\src\RoadwayDesignChecker.Civil3D\bin\x64\Release\net8.0-windows\RoadwayDesignChecker.Civil3D.dll`.
4. Run `CHECKROADWAY` on the command line (or click **Analyze → Roadway QA → Roadway Checker**).
