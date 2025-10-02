# apply_pose_template.py
# MPFB "GameEngine" FK pose applier
# - Blender 4.2+
# - Local XYZ Euler only (degrees → radians inside)
# - Drive *deform bones* directly; no controllers, no keyframes, no loc/scale edits

import bpy, math

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

# Optional toggles
SWAP_LR   = False   # flip L<->R if the reference image is mirrored

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

def _set_euler(arm_obj, bone_name, rx, ry, rz):
    """Apply rotation to the bone."""
    pb = arm_obj.pose.bones.get(bone_name)
    if not pb:
        print(f"[WARN] Missing bone: {bone_name}")
        return
    pb.rotation_mode = 'XYZ'
    pb.rotation_euler = (rx, ry, rz)

def reset_rotations(arm_obj):
    for name in BONES_ORDER:
        pb = arm_obj.pose.bones.get(name)
        if pb:
            pb.rotation_mode = 'XYZ'
            pb.rotation_euler = (0.0, 0.0, 0.0)

def apply_pose(arm_obj, pose_deg):
    for name, (dx, dy, dz) in pose_deg.items():
        name = _swap_lr_key(name)
        _set_euler(arm_obj, name, math.radians(dx), math.radians(dy), math.radians(dz))
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
