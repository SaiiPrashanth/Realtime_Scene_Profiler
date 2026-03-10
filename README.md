# Realtime Scene Profiler

A **Unity Editor** window that scans and profiles every mesh in the active scene, displaying per-object and scene-wide performance stats with a live Scene View overlay.

## Demo

![Demo](ScreenProfiler.gif)

## Overview

Knowing which objects in your scene are the most expensive is the first step to optimizing it. This tool provides an instant breakdown of triangle counts, vertex counts, material counts, and mesh memory for every `MeshFilter` and `SkinnedMeshRenderer` in the scene — all from a single window.

## Features

- **Scene Scan**: Collects stats for every mesh object in the active scene on demand.
- **Scene Totals**: Displays aggregate object count, triangle count, vertex count, and GPU memory at a glance.
- **Sortable Table**: Sort results by Name, Tris, Verts, Materials, or Memory with toggle ascending/descending.
- **Color-Coded Rows**: Yellow for objects over 10,000 tris, red for objects over 50,000 tris (configurable thresholds).
- **Scene View Overlay**:
  - Per-object tri-count labels drawn directly in the 3D viewport.
  - Optional wireframe tint on heavy objects.
  - Configurable label draw distance.
- **Auto-Refresh**: Overlay can refresh automatically on a configurable timer (0.2 – 5 s).
- **Click to Select**: Clicking an object row in the list pings and selects it in the scene.

## Prerequisites

- **Unity** (2021.3 LTS or newer recommended).
- Place `Scripts/RealtimeSceneProfiler.cs` inside an `Editor/` folder in your Unity project.

## Usage

1. Drop `Scripts/RealtimeSceneProfiler.cs` into any `Editor/` folder (e.g., `Assets/Editor/`).
2. Open the window via **Tools > Realtime Scene Profiler**.
3. Click **Scan Scene** to populate the stats table.
4. Enable **Show Overlay in Scene** to see live tri-count labels in the Scene view.

## Configuration

| Setting | Default | Description |
|---|---|---|
| Auto Refresh | Off | Automatically re-scan at the set interval |
| Refresh Interval | `1 s` | Seconds between auto-refresh cycles |
| Show Labels | On | Draw per-object labels in the Scene view |
| Show Wireframe Tint | On | Tint heavy objects in the Scene view |
| Label Distance | `30` | Max camera distance to draw labels |
| Warning Threshold | `10000` | Tris count for yellow highlight |
| Error Threshold | `50000` | Tris count for red highlight |

## Script Reference

### `Scripts/RealtimeSceneProfiler.cs`
- **`ScanScene()`**: Iterates all `MeshFilter` and `SkinnedMeshRenderer` components, collects stats, and computes scene totals.
- **`OnSceneGUI()`**: Draws the Scene View overlay (labels and wireframe tints) via Handles API.
- **`Update()`**: Drives the auto-refresh timer when the overlay is active.
- **`ObjectStats`** struct: Stores Name, Tris, Verts, Materials, MemoryBytes, ShaderName, IsStatic, Bounds per object.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
Copyright (c) 2025 ARGUS
