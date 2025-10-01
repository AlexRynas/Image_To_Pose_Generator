# apply_pose_template.py
# MPFB "GameEngine" FK pose applier (geometry-hinge + overrides)
# - Blender 4.2+
# - Local XYZ Euler only (degrees → radians inside)
# - Drives deform bones directly; no controllers, no keyframes, no loc/scale edits

import bpy, math
from mathutils import Vector

ARMATURE_NAME = "Armature"   # change if your armature object is named differently

# Exact bones from your GameEngine armature report (parent→child order)
BONES_ORDER = [
    "pelvis",
    "spine_01","spine_02","spine_03",
    "neck_01","head",
    "clavicle_l","upperarm_l","lowerarm_l","hand_l",
    "clavicle_r","upperarm_r","lowerarm_r","hand_r",
    "thigh_l","calf_l","foot_l","ball_l",
    "thigh_r","calf_r","foot_r","ball_r",
]

# Options
SWAP_LR            = False     # flip L<->R if the reference image is mirrored
PREFER_Y_AS_FLEX   = True      # write elbow/knee "flex" on Y — script maps it to actual hinge axis
DEBUG_PRINT        = True      # print hinge map for verification
PREVIEW_FLEX_DEG   = 0.0       # put e.g. 20.0 to preview a small bend on all hinge joints (for calibration)

# Hinge-like joints (we compute their actual hinge axis at runtime from rest-pose geometry)
HINGE_PAIRS = [
    ("upperarm_l","lowerarm_l"),  # elbow L
    ("upperarm_r","lowerarm_r"),  # elbow R
    ("thigh_l","calf_l"),         # knee L
    ("thigh_r","calf_r"),         # knee R
]

# Overrides (optional). If set, these replace auto-detected axis / sign.
# Axis values must be 'x', 'y', or 'z'.
HINGE_AXIS_OVERRIDE = {
    # Example: "lowerarm_l": "x", "calf_l": "x"
    # "lowerarm_l": "x", "lowerarm_r": "x",
    # "calf_l": "x",     "calf_r": "x",
}
# Sign multiplier applied to the flex value for a hinge bone (+1 or -1)
HINGE_SIGN_OVERRIDE = {
    # Example: "lowerarm_l": -1, "calf_r": -1
    # "calf_l": -1,
    # "calf_r": -1,
}

# Left/Right swap map
LR_MAP = {
    "clavicle_l":"clavicle_r","upperarm_l":"upperarm_r","lowerarm_l":"lowerarm_r","hand_l":"hand_r",
    "clavicle_r":"clavicle_l","upperarm_r":"upperarm_l","lowerarm_r":"lowerarm_l","hand_r":"hand_l",
    "thigh_l":"thigh_r","calf_l":"calf_r","foot_l":"foot_r","ball_l":"ball_r",
    "thigh_r":"thigh_l","calf_r":"calf_l","foot_r":"foot_l","ball_r":"ball_l",
}

def _ensure_pose_mode(obj):
    bpy.context.view_layer.objects.active = obj
    if obj.mode != 'POSE':
        bpy.ops.object.mode_set(mode='POSE')

def _swap_lr_key(name):
    return LR_MAP.get(name, name) if SWAP_LR else name

def _vec_from_bone(db):
    """Direction vector (armature space) from head->tail in rest pose."""
    v = (db.tail_local - db.head_local)
    if v.length > 1e-8:
        v.normalize()
    return v

def _axes_from_bone(db):
    """Return local XYZ unit axes of the bone in armature space."""
    M3 = db.matrix_local.to_3x3()
    x = M3.col[0].normalized()
    y = M3.col[1].normalized()
    z = M3.col[2].normalized()
    return x, y, z

def _compute_hinge_axis_for_child(parent_db, child_db):
    """Infer hinge axis for the child bone: normal to the plane spanned by parent and child directions.
    Returns index 0/1/2 meaning child's local X/Y/Z best aligned with that normal.
    """
    up = _vec_from_bone(parent_db)   # parent direction
    uc = _vec_from_bone(child_db)    # child direction
    normal = up.cross(uc)
    if normal.length < 1e-8:
        # Parent and child are nearly collinear. Choose axis ⟂ child direction, prefer child's local X or Z.
        x, y, z = _axes_from_bone(child_db)
        dots = [abs(x.dot(uc)), abs(y.dot(uc)), abs(z.dot(uc))]
        idx = min(range(3), key=lambda i: dots[i])
        return idx
    normal.normalize()
    x, y, z = _axes_from_bone(child_db)
    scores = [abs(x.dot(normal)), abs(y.dot(normal)), abs(z.dot(normal))]
    idx = max(range(3), key=lambda i: scores[i])
    return idx

def _build_hinge_map(arm_obj):
    """Return dict: child_name -> 'x'|'y'|'z' based on geometry + overrides."""
    amap = {}
    bones = arm_obj.data.bones
    for p, c in HINGE_PAIRS:
        p = _swap_lr_key(p); c = _swap_lr_key(c)
        parent_db = bones.get(p); child_db = bones.get(c)
        if not parent_db or not child_db:
            continue
        axis = HINGE_AXIS_OVERRIDE.get(c)
        if axis not in ('x','y','z',None):
            axis = None
        if axis is None:
            idx = _compute_hinge_axis_for_child(parent_db, child_db)
            axis = ('x','y','z')[idx]
        amap[c] = axis
    return amap

def _apply_flex_on_axis(pbone, axis, radians_value):
    cur = list(pbone.rotation_euler)
    if axis == 'x': cur[0] += radians_value
    elif axis == 'y': cur[1] += radians_value
    else: cur[2] += radians_value
    pbone.rotation_euler = tuple(cur)

def _set_euler(arm_obj, bone_name, rx, ry, rz, hinge_map):
    pb = arm_obj.pose.bones.get(bone_name)
    if not pb:
        print(f"[WARN] Missing bone: {bone_name}")
        return
    pb.rotation_mode = 'XYZ'
    axis = hinge_map.get(bone_name)
    if axis and PREFER_Y_AS_FLEX:
        # Map the user's Y (flex) to the detected/overridden hinge axis. Allow per-bone sign override.
        sign = HINGE_SIGN_OVERRIDE.get(bone_name, 1.0)
        flex = sign * (ry if abs(ry) > 1e-6 else (rx if abs(rx) >= abs(rz) else rz))
        _apply_flex_on_axis(pb, axis, flex)
    else:
        pb.rotation_euler = (rx, ry, rz)

def reset_rotations(arm_obj):
    for name in BONES_ORDER:
        pb = arm_obj.pose.bones.get(name)
        if pb:
            pb.rotation_mode = 'XYZ'
            pb.rotation_euler = (0.0, 0.0, 0.0)

def apply_pose(arm_obj, pose_deg):
    hinge_map = _build_hinge_map(arm_obj)
    if DEBUG_PRINT:
        print("Hinge map (computed/overridden):", hinge_map)
    # Optional preview flex for calibration
    if PREVIEW_FLEX_DEG and PREVIEW_FLEX_DEG != 0.0:
        for _, child in HINGE_PAIRS:
            bn = _swap_lr_key(child)
            pb = arm_obj.pose.bones.get(bn)
            if not pb: 
                continue
            pb.rotation_mode = 'XYZ'
            axis = hinge_map.get(bn)
            if not axis: 
                continue
            sign = HINGE_SIGN_OVERRIDE.get(bn, 1.0)
            _apply_flex_on_axis(pb, axis, math.radians(sign * PREVIEW_FLEX_DEG))
        bpy.context.view_layer.update()
        print(f"[PREVIEW] Applied {PREVIEW_FLEX_DEG}° test flex on hinge axes. Set PREVIEW_FLEX_DEG=0 to disable.")
        return

    # Normal apply
    for name, (dx, dy, dz) in pose_deg.items():
        name = _swap_lr_key(name)
        _set_euler(arm_obj, name, math.radians(dx), math.radians(dy), math.radians(dz), hinge_map)
    bpy.context.view_layer.update()

# ---- FILL THIS ONLY (degrees) ----
POSE_DEGREES = {
    # torso
    "pelvis":   [0.0, 0.0, 0.0],
    "spine_01": [0.0, 0.0, 0.0],
    "spine_02": [0.0, 0.0, 0.0],
    "spine_03": [0.0, 0.0, 0.0],

    # neck / head
    "neck_01":  [0.0, 0.0, 0.0],
    "head":     [0.0, 0.0, 0.0],

    # left arm
    "clavicle_l":[0.0, 0.0, 0.0],
    "upperarm_l":[0.0, 0.0, 0.0],
    "lowerarm_l":[0.0, 0.0, 0.0],
    "hand_l":   [0.0, 0.0, 0.0],

    # right arm
    "clavicle_r":[0.0, 0.0, 0.0],
    "upperarm_r":[0.0, 0.0, 0.0],
    "lowerarm_r":[0.0, 0.0, 0.0],
    "hand_r":   [0.0, 0.0, 0.0],

    # left leg
    "thigh_l":  [0.0, 0.0, 0.0],
    "calf_l":   [0.0, 0.0, 0.0],
    "foot_l":   [0.0, 0.0, 0.0],
    "ball_l":   [0.0, 0.0, 0.0],

    # right leg
    "thigh_r":  [0.0, 0.0, 0.0],
    "calf_r":   [0.0, 0.0, 0.0],
    "foot_r":   [0.0, 0.0, 0.0],
    "ball_r":   [0.0, 0.0, 0.0],
}

def main():
    arm = bpy.data.objects.get(ARMATURE_NAME)
    if not arm or arm.type != 'ARMATURE':
        raise RuntimeError(f"Armature '{ARMATURE_NAME}' not found")
    _ensure_pose_mode(arm)
    reset_rotations(arm)
    apply_pose(arm, POSE_DEGREES)
    print("[apply_pose_template_gameengine] Pose applied.")

if __name__ == "__main__":
    main()
