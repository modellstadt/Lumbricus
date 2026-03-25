# Lumbricus

Grasshopper plugin for 3D print toolpath visualization. Generates mesh representations of extruded bead geometry by sweeping a nozzle profile along toolpath polylines.

Bridges the gap between abstract print paths and the actual extruded result — see exactly what will be deposited before printing.

## Features

- **Profile Sweep** — sweep any closed polyline profile (circle, ellipse, rounded rectangle) along one or multiple toolpath polylines
- **Build Progress** — animate the printing process with a 0–1 slider that walks through all paths sequentially
- **Custom Nozzle Profiles** — match the exact cross-section of your hardware for accurate bead representation
- **End Caps** — optional capping of open path ends with triangulated faces
- **UV Mapping** — generate texture coordinates for material visualization (U along path, V across profile)
- **Built-in Demo** — connect nothing and get a parametric 30-layer vase to explore immediately

## Component

**Profile Sweep** (Category: Lumbricus > 3DPrint)

### Inputs

| Parameter | Type | Description |
|---|---|---|
| **P** | Polylines | Toolpath polylines (optional — shows demo vase when empty) |
| **Pr** | Polyline | Closed nozzle profile (optional — uses default elliptical bead) |
| **Rev** | Boolean | Reverse profile winding |
| **C** | Boolean | Cap open path ends (default: on) |
| **UV** | Boolean | Generate texture coordinates (default: off) |
| **T** | Number | Build progress 0.0–1.0 (default: 1.0) |

### Outputs

| Parameter | Type | Description |
|---|---|---|
| **M** | Meshes | Swept meshes (one per toolpath) |
| **V** | String | Version string |

## Use Cases

- Preview extrusion-based 3D prints (FDM, robotic clay, concrete)
- Validate layer stacking and bead overlap before fabrication
- Animate the build sequence for presentations or documentation
- Generate mesh geometry for rendering or further analysis

## Requirements

Rhino 8 / Grasshopper 8
