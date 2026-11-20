# Civil 3D Programming, Plugins & Add-ins

A collection of **Autodesk Civil 3D** programming tools, **Dynamo** scripts, **.NET/C#** add-ins, and plugins for civil engineering and infrastructure automation.

## Repository Structure

```text
Civil3d/
├── dynamo/          # Dynamo for Civil 3D graphs (.dyn) organized by workflow
├── addins/          # Civil 3D .NET / C# API add-ins and Visual Studio solutions
├── plugins/         # Packaged Civil 3D plugins and bundle manifests (.bundle)
└── README.md
```

## Dynamo Scripts

| Category | Script | Description |
| :--- | :--- | :--- |
| [01 - Import and Create Point Groups](dynamo/01-import-and-create-point-groups/README.md) | [`PENZD-Space Delimited.dyn`](dynamo/01-import-and-create-point-groups/PENZD-Space%20Delimited.dyn) | Batch-imports space-delimited **PENZD** (Point, Easting, Northing, Elevation, Description) `.txt`/`.csv` files, creates Civil 3D COGO points, and organizes each file into a Point Group named after the source file. |
| [01 - Import and Create Point Groups](dynamo/01-import-and-create-point-groups/README.md) | [`PNEZD-Space Delimited.dyn`](dynamo/01-import-and-create-point-groups/PNEZD-Space%20Delimited.dyn) | Batch-imports space-delimited **PNEZD** (Point, Northing, Easting, Elevation, Description) `.txt`/`.csv` files, creates Civil 3D COGO points, and organizes each file into a Point Group named after the source file. |

## Add-ins & Plugins

| Tool | Source | Bundle | Description |
| :--- | :--- | :--- | :--- |
| **Roadway Design Checker (SHC 301 & AASHTO)** | [`addins/RoadwayDesignChecker`](addins/RoadwayDesignChecker/README.md) | [`plugins/RoadwayDesignChecker.bundle`](plugins/RoadwayDesignChecker.bundle/PackageContents.xml) | Native **.NET 8** Civil 3D 2025/2026 plugin with a human-readable dockable WPF palette that audits horizontal alignments, vertical profiles, superelevation, and sight distance against **Saudi Highway Code (SHC 301)**, **AASHTO 2018 (Metric)**, Civil 3D Criteria XML, and custom JSON overrides, with one-click **Zoom-to-Station** and HTML/CSV/JSON report export. |

- `addins/` — Source code for Civil 3D .NET API (`Autodesk.Civil.ApplicationServices`, `Autodesk.AutoCAD.*`) add-ins.
- `plugins/` — Ready-to-deploy `.bundle` folders and `PackageContents.xml` manifests.

## Prerequisites

- **Autodesk Civil 3D** (built with Dynamo for Civil 3D `3.6.1`; uses built-in Civil 3D/AutoCAD Dynamo nodes with no external third-party packages required)
- **Visual Studio / .NET SDK** (for building C#/.NET add-ins)