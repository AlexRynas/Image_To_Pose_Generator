# Robust armature exporter for Blender 4.2+
# Exports:
#  - armature object + world matrix
#  - per-pose-bone: name, parent, deform, rotation_mode, loc/rot/scale,
#    matrix_basis, pose-space matrix, world matrix
#  - rest data: head_local, tail_local, length, matrix_local  (NO 'roll')
#  - constraints (safe subset per type)
#  - custom properties (armature + pose bones)
# Writes JSON to a Text datablock and, if possible, next to the .blend file.

import bpy
import json
import os
from mathutils import Matrix, Vector, Quaternion

def _matrix_to_flat_list(M: Matrix):
    try:
        rows = len(M)
        cols = len(M[0]) if rows else 0
        return [M[i][j] for i in range(rows) for j in range(cols)]
    except Exception:
        # Fallback: try to force 4x4
        MF = Matrix(M)
        return [MF[i][j] for i in range(4) for j in range(4)]

def _to_jsonable(v):
    if isinstance(v, Vector):
        return list(v)
    if isinstance(v, Quaternion):
        return [v.w, v.x, v.y, v.z]
    if isinstance(v, Matrix):
        return _matrix_to_flat_list(v)
    if isinstance(v, (list, tuple)):
        return [ _to_jsonable(x) for x in v ]
    if isinstance(v, (int, float, str, bool)) or v is None:
        return v
    # Unknown / non-serializable: stringify
    return str(v)

def _safe_name(x):
    try:
        return x.name
    except Exception:
        return None

def _custom_props(idblock):
    data = {}
    try:
        for k in idblock.keys():
            if k == "_RNA_UI":
                continue
            val = idblock.get(k)
            if isinstance(val, (int, float, str, bool)):
                data[k] = val
            elif isinstance(val, (list, tuple)) and all(isinstance(x, (int, float, str, bool)) for x in val):
                data[k] = list(val)
            # else skip exotic types
    except Exception:
        pass
    return data

def _constraint_brief(c):
    d = {
        "name": getattr(c, "name", None),
        "type": getattr(c, "type", None),
        "mute": bool(getattr(c, "mute", False)),
        "influence": float(getattr(c, "influence", 1.0)),
        "owner_space": getattr(c, "owner_space", None),
        "target_space": getattr(c, "target_space", None),
        "target_object": _safe_name(getattr(c, "target", None)),
        "subtarget": getattr(c, "subtarget", None),
    }

    def add(attr, conv=None):
        if hasattr(c, attr):
            val = getattr(c, attr)
            d[attr] = conv(val) if conv else val

    t = d["type"]

    if t == 'LIMIT_ROTATION':
        for a in ("use_limit_x","use_limit_y","use_limit_z",
                  "min_x","min_y","min_z","max_x","max_y","max_z",
                  "use_transform_limit"):
            add(a, float if a.startswith(("min_","max_")) else None)
    elif t == 'LIMIT_LOCATION':
        for a in ("use_min_x","use_min_y","use_min_z",
                  "use_max_x","use_max_y","use_max_z",
                  "min_x","min_y","min_z","max_x","max_y","max_z"):
            add(a, float if a.startswith(("min_","max_")) else None)
    elif t == 'LIMIT_SCALE':
        for a in ("use_min_x","use_min_y","use_min_z",
                  "use_max_x","use_max_y","use_max_z",
                  "min_x","min_y","min_z","max_x","max_y","max_z",
                  "use_transform_limit"):
            add(a, float if a.startswith(("min_","max_")) else None)
    elif t == 'COPY_ROTATION':
        for a in ("use_x","use_y","use_z","invert_x","invert_y","invert_z","mix_mode"):
            add(a)
        add("target", _safe_name)
        add("subtarget")
    elif t == 'DAMPED_TRACK':
        add("track_axis")
        add("target", _safe_name)
        add("subtarget")
    elif t == 'IK':
        for a in ("chain_count","use_rotation","use_stretch","use_tail","weight",
                  "pole_angle","iterations","lock_x","lock_y","lock_z"):
            add(a)
        add("pole_target", _safe_name)
        add("pole_subtarget")
        add("target", _safe_name)
        add("subtarget")

    return d

def export_armature_info(arm_obj, filepath=None):
    if arm_obj is None or arm_obj.type != 'ARMATURE':
        raise RuntimeError("Active object must be an ARMATURE.")

    bpy.context.view_layer.update()
    scene = bpy.context.scene

    out = {
        "blender_version": bpy.app.version_string,
        "unit_scale": getattr(scene.unit_settings, "scale_length", 1.0),
        "armature_object": arm_obj.name,
        "armature_world_matrix": _to_jsonable(arm_obj.matrix_world.copy()),
        "bones": [],
        "armature_custom_properties": _custom_props(arm_obj),
    }

    for pb in arm_obj.pose.bones:
        b = pb.bone  # rest data carrier (NOT EditBone)
        rest = {
            "head_local": _to_jsonable(getattr(b, "head_local", Vector())),
            "tail_local": _to_jsonable(getattr(b, "tail_local", Vector())),
            "length": float(getattr(b, "length", 0.0)),
            "matrix_local": _to_jsonable(getattr(b, "matrix_local", Matrix.Identity(4))),
            # NOTE: 'roll' intentionally omitted (only on EditBone / not accessible here)
        }

        entry = {
            "name": pb.name,
            "parent": pb.parent.name if pb.parent else None,
            "deform": bool(getattr(b, "use_deform", True)),
            "rotation_mode": pb.rotation_mode,
            "location": _to_jsonable(pb.location.copy()),
            "scale": _to_jsonable(pb.scale.copy()),
            "rotation_euler": _to_jsonable(pb.rotation_euler.copy()) if pb.rotation_mode != 'QUATERNION' else None,
            "rotation_quaternion": _to_jsonable(pb.rotation_quaternion.copy()),
            "matrix_basis": _to_jsonable(pb.matrix_basis.copy()),
            "matrix_pose_space": _to_jsonable(pb.matrix.copy()),
            "matrix_world": _to_jsonable((arm_obj.matrix_world @ pb.matrix).copy()),
            "rest": rest,
            "constraints": [],
            "custom_properties": _custom_props(pb),
        }

        for c in pb.constraints:
            try:
                entry["constraints"].append(_constraint_brief(c))
            except Exception:
                # Be resilient to exotic constraints
                entry["constraints"].append({
                    "name": getattr(c, "name", None),
                    "type": getattr(c, "type", None),
                    "error": "unserializable_fields",
                })

        out["bones"].append(entry)

    json_str = json.dumps(out, indent=2)

    # Text datablock
    text_name = f"{arm_obj.name}_armature_report.json"
    txt = bpy.data.texts.get(text_name) or bpy.data.texts.new(text_name)
    txt.clear()
    txt.write(json_str)

    # File output (if possible)
    out_path = filepath
    if not out_path:
        blend_dir = bpy.path.abspath("//")
        if blend_dir and os.path.isdir(blend_dir):
            out_path = os.path.join(blend_dir, text_name)

    if out_path:
        try:
            with open(out_path, "w", encoding="utf-8") as f:
                f.write(json_str)
            print(f"[Armature Export v1.1] Wrote JSON to: {out_path}")
        except Exception as e:
            print(f"[Armature Export v1.1] Could not write file: {e}")
    else:
        print("[Armature Export v1.1] .blend not saved; JSON stored in Text datablock only.")

    return out

def main():
    arm = bpy.context.active_object
    if arm is None or arm.type != 'ARMATURE':
        raise RuntimeError("Select an armature object and make it Active before running.")
    export_armature_info(arm)

if __name__ == "__main__":
    main()
