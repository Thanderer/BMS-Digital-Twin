"""
SafeCharge — Blender asset builder
==================================

Generates the 3D assets for the SafeCharge BMS Digital Twin and exports them
as a single .fbx file ready to drop into Unity.

Objects produced (each in its own Collection so Unity import keeps them grouped):

    pack_shell      — outer prismatic module casing (the box you see in the scene)
    cell_00 … cell_15 — 16 prismatic cells in a 4x4 grid inside the casing
                        (the digit indices match the SOH array in Unity)
    fan_housing     — 3D fan housing attached to the side of the pack
    fan_rotor       — 5-blade rotor, separate so Unity can spin it
    cross_section_anode / cross_section_separator / cross_section_cathode
                    — flat strips for the educational cell-internals view

Conventions:
    Z up, 1 Blender unit = 1 metre, transforms applied, single material
    slot per object (Unity will replace materials anyway).

Headless usage (no Blender GUI needed):
    blender --background --python build_pack.py -- --rows 4 --cols 4 --out pack.fbx

CLI flags:
    --rows N            number of cell rows (default 4)
    --cols N            number of cell columns (default 4)
    --cell-w X          single cell width  in metres (default 0.034)
    --cell-h X          single cell height in metres (default 0.090)
    --cell-d X          single cell depth  in metres (default 0.140)
    --gap X             gap between cells in metres (default 0.004)
    --shell-thickness X casing wall thickness in metres (default 0.006)
    --no-fan            skip the cooling fan
    --no-cross-section  skip the cell cross-section asset
    --out PATH          output .fbx path (default pack.fbx in CWD)

Author: SafeCharge — Group 6, OVGU Magdeburg, Summer 2026.
"""

import argparse
import math
import os
import sys
import bpy
import bmesh
import mathutils


# -----------------------------------------------------------------------------
# CLI parsing (Blender forwards everything after "--" to sys.argv)
# -----------------------------------------------------------------------------

def parse_args():
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = []

    p = argparse.ArgumentParser(description="Build the SafeCharge battery pack assets.")
    p.add_argument("--rows", type=int, default=4)
    p.add_argument("--cols", type=int, default=4)
    p.add_argument("--cell-w", type=float, default=0.034, help="cell width, m")
    p.add_argument("--cell-h", type=float, default=0.090, help="cell height, m")
    p.add_argument("--cell-d", type=float, default=0.140, help="cell depth, m")
    p.add_argument("--gap",     type=float, default=0.004, help="gap between cells, m")
    p.add_argument("--shell-thickness", type=float, default=0.006)
    p.add_argument("--no-fan", action="store_true")
    p.add_argument("--no-cross-section", action="store_true")
    p.add_argument("--out", type=str, default="pack.fbx",
                   help="output .fbx path (relative paths resolve to CWD)")
    return p.parse_args(argv)


# -----------------------------------------------------------------------------
# Scene management
# -----------------------------------------------------------------------------

def reset_scene():
    """Strip the default cube / camera / light so we start from an empty scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # Wipe any orphan meshes/materials/collections from a previous run.
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.collections):
        for item in list(block):
            if item.users == 0:
                block.remove(item)
    # Configure scene for metres and Z-up (FBX exporter respects these).
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0


def make_collection(name):
    """Create (or fetch) a top-level collection by name."""
    if name in bpy.data.collections:
        return bpy.data.collections[name]
    coll = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(coll)
    return coll


def link_to_collection(obj, coll):
    """Move obj into coll, removing from any other collections."""
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def apply_transforms(obj):
    """Bake location/rotation/scale into the mesh so Unity gets clean data."""
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


# -----------------------------------------------------------------------------
# Mesh primitives (built with bmesh for predictable topology)
# -----------------------------------------------------------------------------

def make_box(name, size, center=(0, 0, 0)):
    """Create an axis-aligned box mesh of given size centred at `center`."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()
    obj.scale = size
    obj.location = center
    return obj


def make_hollow_shell(name, outer_size, thickness, center=(0, 0, 0)):
    """
    Create an outer box and use a boolean subtraction to hollow out the inside,
    leaving walls of `thickness` on all six sides. The top face is also removed
    so the cells are visible from above when the model is rotated.
    """
    outer = make_box(name + "_outer", outer_size, center)
    inner_size = (
        max(0.001, outer_size[0] - 2 * thickness),
        max(0.001, outer_size[1] - 2 * thickness),
        max(0.001, outer_size[2] - thickness),  # leave only the bottom + 4 sides
    )
    inner_center = (center[0], center[1], center[2] + thickness * 0.5)
    inner = make_box(name + "_inner", inner_size, inner_center)
    apply_transforms(outer)
    apply_transforms(inner)
    # Boolean modifier: outer minus inner.
    mod = outer.modifiers.new("Hollow", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.object = inner
    bpy.context.view_layer.objects.active = outer
    bpy.ops.object.modifier_apply(modifier="Hollow")
    # Delete the helper inner box.
    bpy.data.objects.remove(inner, do_unlink=True)
    outer.name = name
    return outer


def make_cylinder(name, radius, depth, segments=24, center=(0, 0, 0), axis='Y'):
    """A capped cylinder along an axis (X, Y, or Z)."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=segments,
        radius1=radius,
        radius2=radius,
        depth=depth,
    )
    bm.to_mesh(mesh)
    bm.free()
    obj.location = center
    # Default cylinder is along Z. Rotate if we want a different axis.
    if axis == 'X':
        obj.rotation_euler = (0, math.radians(90), 0)
    elif axis == 'Y':
        obj.rotation_euler = (math.radians(90), 0, 0)
    return obj


# -----------------------------------------------------------------------------
# Materials (placeholder — Unity rebinds anyway)
# -----------------------------------------------------------------------------

def make_material(name, rgba):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat.diffuse_color = rgba
    mat.use_nodes = False  # keep it lean — Unity replaces this on import
    return mat


def assign_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# -----------------------------------------------------------------------------
# Asset constructors
# -----------------------------------------------------------------------------

def build_pack_shell(args, coll, materials):
    """Outer hollow casing sized to fit the 4x4 cell grid + a little margin."""
    inner_w = args.cols * args.cell_w + (args.cols - 1) * args.gap
    inner_d = args.rows * args.cell_d + (args.rows - 1) * args.gap
    margin = args.shell_thickness * 2

    outer_w = inner_w + 2 * args.shell_thickness + margin
    outer_h = args.cell_h + 2 * args.shell_thickness
    outer_d = inner_d + 2 * args.shell_thickness + margin

    shell = make_hollow_shell(
        "pack_shell",
        outer_size=(outer_w, outer_d, outer_h),
        thickness=args.shell_thickness,
        center=(0, 0, outer_h / 2),
    )
    apply_transforms(shell)
    assign_material(shell, materials["shell"])
    link_to_collection(shell, coll)
    return shell, (inner_w, inner_d, args.cell_h)


def build_cells(args, coll, materials):
    """A flat 4x4 grid of prismatic cells named cell_00 .. cell_15."""
    inner_w = args.cols * args.cell_w + (args.cols - 1) * args.gap
    inner_d = args.rows * args.cell_d + (args.rows - 1) * args.gap

    # Bottom-left corner of the grid, in world coordinates.
    x0 = -inner_w / 2 + args.cell_w / 2
    y0 = -inner_d / 2 + args.cell_d / 2
    z = args.cell_h / 2 + args.shell_thickness  # rest on the shell floor

    cells = []
    for r in range(args.rows):
        for c in range(args.cols):
            idx = r * args.cols + c
            name = f"cell_{idx:02d}"
            cx = x0 + c * (args.cell_w + args.gap)
            cy = y0 + r * (args.cell_d + args.gap)
            cell = make_box(
                name,
                size=(args.cell_w, args.cell_d, args.cell_h),
                center=(cx, cy, z),
            )
            apply_transforms(cell)
            assign_material(cell, materials["cell"])
            link_to_collection(cell, coll)
            cells.append(cell)
    return cells


def build_fan(args, coll, materials, pack_inner_dims):
    """
    A fan unit (housing + 5-blade rotor) bolted to the +Y face of the pack.

    The rotor is a separate object so Unity can spin it via a transform animation.
    """
    inner_w, inner_d, inner_h = pack_inner_dims
    fan_radius = min(inner_h * 0.45, inner_w * 0.18)
    fan_depth = 0.020
    # Fan sits just beyond the +Y end of the pack.
    fan_y = inner_d / 2 + args.shell_thickness + fan_depth / 2
    fan_z = inner_h / 2 + args.shell_thickness

    housing = make_cylinder(
        "fan_housing", radius=fan_radius * 1.1,
        depth=fan_depth, segments=32,
        center=(0, fan_y, fan_z), axis='Y',
    )
    apply_transforms(housing)
    assign_material(housing, materials["fan_housing"])
    link_to_collection(housing, coll)

    # Rotor hub.
    rotor_hub = make_cylinder(
        "fan_rotor", radius=fan_radius * 0.18,
        depth=fan_depth * 0.9, segments=18,
        center=(0, fan_y, fan_z), axis='Y',
    )
    apply_transforms(rotor_hub)

    # Five blades, joined into the rotor hub mesh.
    bpy.ops.object.select_all(action='DESELECT')
    rotor_hub.select_set(True)
    bpy.context.view_layer.objects.active = rotor_hub
    for i in range(5):
        angle = (2 * math.pi / 5) * i
        # A flat box that we then rotate around Y (fan axis).
        blade_len = fan_radius * 0.85
        blade = make_box(
            f"_blade_{i}",
            size=(blade_len, fan_depth * 0.6, fan_radius * 0.18),
            center=(blade_len / 2, fan_y, fan_z),
        )
        # Rotate the blade around the fan axis (the +Y axis through fan centre).
        rot = mathutils.Matrix.Rotation(angle, 4, 'Y')
        # Pivot around the rotor centre, not the world origin.
        pivot = mathutils.Vector((0, fan_y, fan_z))
        blade.matrix_world = (
            mathutils.Matrix.Translation(pivot) @ rot @
            mathutils.Matrix.Translation(-pivot) @ blade.matrix_world
        )
        apply_transforms(blade)
        blade.select_set(True)

    rotor_hub.select_set(True)
    bpy.context.view_layer.objects.active = rotor_hub
    bpy.ops.object.join()  # merges all selected meshes into the active rotor_hub
    rotor_hub.name = "fan_rotor"
    assign_material(rotor_hub, materials["fan_rotor"])
    link_to_collection(rotor_hub, coll)
    return housing, rotor_hub


def build_cross_section(args, coll, materials):
    """
    A small didactic asset placed beside the pack: three thin slabs (anode,
    separator, cathode) ready to receive an ion-flow particle system in Sprint 4.
    """
    slab_h = 0.060
    slab_d = 0.080
    anode_w = 0.012
    sep_w   = 0.003
    cathode_w = 0.012
    total_w = anode_w + sep_w + cathode_w

    # Place to the side (negative X) so it doesn't collide with the pack.
    base_x = -0.35 - total_w / 2
    base_z = slab_h / 2

    cx = base_x + anode_w / 2
    anode = make_box("cross_section_anode",
                     size=(anode_w, slab_d, slab_h),
                     center=(cx, 0, base_z))
    apply_transforms(anode)
    assign_material(anode, materials["anode"])
    link_to_collection(anode, coll)

    cx += anode_w / 2 + sep_w / 2
    sep = make_box("cross_section_separator",
                   size=(sep_w, slab_d, slab_h),
                   center=(cx, 0, base_z))
    apply_transforms(sep)
    assign_material(sep, materials["separator"])
    link_to_collection(sep, coll)

    cx += sep_w / 2 + cathode_w / 2
    cathode = make_box("cross_section_cathode",
                       size=(cathode_w, slab_d, slab_h),
                       center=(cx, 0, base_z))
    apply_transforms(cathode)
    assign_material(cathode, materials["cathode"])
    link_to_collection(cathode, coll)

    return [anode, sep, cathode]


# -----------------------------------------------------------------------------
# FBX export
# -----------------------------------------------------------------------------

def export_fbx(path):
    """Export everything in the scene as an .fbx Unity will accept."""
    abs_path = os.path.abspath(path)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=abs_path,
        use_selection=False,
        global_scale=1.0,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',                # Unity convention
        bake_space_transform=True,
        object_types={'MESH'},
        use_mesh_modifiers=True,
        mesh_smooth_type='OFF',
        path_mode='COPY',
        embed_textures=False,
    )
    return abs_path


# -----------------------------------------------------------------------------
# Main
# -----------------------------------------------------------------------------

def main():
    args = parse_args()

    print("\n--- SafeCharge build_pack.py ---")
    print(f"  rows x cols : {args.rows} x {args.cols}")
    print(f"  cell        : {args.cell_w*1000:.1f} x {args.cell_h*1000:.1f} x {args.cell_d*1000:.1f} mm")
    print(f"  output      : {os.path.abspath(args.out)}\n")

    reset_scene()

    # Materials — placeholder colours, replaced inside Unity.
    materials = {
        "shell":       make_material("PackShell",      (0.62, 0.62, 0.66, 1.0)),
        "cell":        make_material("Cell",           (0.28, 0.42, 0.60, 1.0)),
        "fan_housing": make_material("FanHousing",     (0.30, 0.30, 0.32, 1.0)),
        "fan_rotor":   make_material("FanRotor",       (0.85, 0.85, 0.87, 1.0)),
        "anode":       make_material("Anode",          (0.30, 0.55, 0.80, 1.0)),
        "separator":   make_material("Separator",      (0.85, 0.85, 0.85, 1.0)),
        "cathode":     make_material("Cathode",        (0.78, 0.45, 0.20, 1.0)),
    }

    pack_coll  = make_collection("Pack")
    cell_coll  = make_collection("Cells")
    fan_coll   = make_collection("Fan")
    xs_coll    = make_collection("CrossSection")

    # 1. Outer shell.
    shell, pack_inner_dims = build_pack_shell(args, pack_coll, materials)

    # 2. Cells.
    cells = build_cells(args, cell_coll, materials)

    # 3. Fan (optional).
    if not args.no_fan:
        build_fan(args, fan_coll, materials, pack_inner_dims)

    # 4. Cell cross-section (optional).
    if not args.no_cross_section:
        build_cross_section(args, xs_coll, materials)

    # Apply any remaining transforms one more time, just in case.
    bpy.ops.object.select_all(action='SELECT')
    for obj in bpy.context.selected_objects:
        if obj.type == 'MESH':
            try:
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            except Exception:
                pass

    out_path = export_fbx(args.out)
    print(f"\n[OK] Wrote {out_path}")
    print(f"     pack_shell + {len(cells)} cells"
          + ("" if args.no_fan else " + fan_housing + fan_rotor")
          + ("" if args.no_cross_section else " + cross_section_*")
          + "\n")


if __name__ == "__main__":
    main()
