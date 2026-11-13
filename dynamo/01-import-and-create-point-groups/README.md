# 01 - Import and Create Point Groups

Dynamo for Civil 3D scripts for batch-importing survey point files (`.txt` / `.csv`) and automatically creating **COGO Points** and **Point Groups** in the active Civil 3D drawing.

## How Both Scripts Work

1. **Multi-File Selection:** Opens a Windows file dialog (`OpenFileDialog` with `Multiselect = True`) allowing you to select one or more `.txt` or `.csv` point files at once.
2. **Parsing:** Reads each file, splits lines on newlines (`\n`), and splits fields on spaces (`' '`).
3. **COGO Point Creation:** Creates a `CogoPoint` on layer `"0"` in the current Civil 3D document (`Document.Current`), assigning the parsed description to both the point's `name` and `rawDescription`.
4. **Point Group Creation:** Groups all points imported from each file into a `CogoPointGroup` named after the source filename (without its extension) with the description `"Created via Dynamo"`.

## Scripts

### [`PENZD-Space Delimited.dyn`](PENZD-Space%20Delimited.dyn)
- **Point Format:** `P E N Z D` (space-delimited)
  - Column `0`: Point Number (`P`)
  - Column `1`: Easting (`E` → `X`)
  - Column `2`: Northing (`N` → `Y`)
  - Column `3`: Elevation (`Z` → `Z`)
  - Column `4`: Description (`D`)
- **Dynamo Version:** `3.6.1`
- **Dependencies:** Built-in Civil 3D & Dynamo nodes only (`Autodesk.Civil.DynamoNodes`, `Autodesk.AutoCAD.DynamoNodes`, `DSCore`, `System.Windows.Forms`). No external packages required.

### [`PNEZD-Space Delimited.dyn`](PNEZD-Space%20Delimited.dyn)
- **Point Format:** `P N E Z D` (space-delimited)
  - Column `0`: Point Number (`P`)
  - Column `1`: Northing (`N` → `Y`)
  - Column `2`: Easting (`E` → `X`)
  - Column `3`: Elevation (`Z` → `Z`)
  - Column `4`: Description (`D`)
- **Dynamo Version:** `3.6.1`
- **Dependencies:** Built-in Civil 3D & Dynamo nodes only (`Autodesk.Civil.DynamoNodes`, `Autodesk.AutoCAD.DynamoNodes`, `DSCore`, `System.Windows.Forms`). No external packages required.
