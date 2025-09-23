# apply_pose_template.py — FK pose applier for your "Armature"
# Works in Blender 4.x. It only sets rotations (no location/scale), no IK, no fingers.

import bpy, math

ARMATURE_NAME = "Armature"

# Bones to drive (head, neck, torso, arms, legs — no fingers)
BONES_ORDER = [
    # pelvis/torso
    "Hips", "Spine", "Spine1", "Spine2", "Spine3", "Neck", "Neck1", "Head",
    # left arm
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    # right arm
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    # left leg
    "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
    # right leg
    "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
]

# For these bones we only write the hinge axis (Y) to respect your Limit Rotation constraint.
HINGE_Y_ONLY = {"LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg"}

# === PUT YOUR POSE HERE (degrees, local XYZ) ===
# Example below is just a neutral-ish placeholder; the AI agent will overwrite these.
POSE_DEGREES = {
    # pelvis & spine (sample numbers; replace)
    "Hips":        [0.0, 0.0, 0.0],
    "Spine":       [0.0, 0.0, 0.0],
    "Spine1":      [0.0, 0.0, 0.0],
    "Spine2":      [0.0, 0.0, 0.0],
    "Spine3":      [0.0, 0.0, 0.0],
    "Neck":        [0.0, 0.0, 0.0],
    "Neck1":       [0.0, 0.0, 0.0],
    "Head":        [0.0, 0.0, 0.0],

    # left arm
    "LeftShoulder":[0.0, 0.0, 0.0],
    "LeftArm":     [0.0, 0.0, 0.0],
    "LeftForeArm": [0.0, 30.0, 0.0],  # elbow flex ~30° (Y only)
    "LeftHand":    [0.0, 0.0, 0.0],

    # right arm
    "RightShoulder":[0.0, 0.0, 0.0],
    "RightArm":     [0.0, 0.0, 0.0],
    "RightForeArm": [0.0, 30.0, 0.0], # elbow flex ~30° (Y only)
    "RightHand":    [0.0, 0.0, 0.0],

    # left leg
    "LeftUpLeg":   [0.0, 0.0, 0.0],
    "LeftLeg":     [0.0, 5.0, 0.0],   # knee slight bend (Y only)
    "LeftFoot":    [0.0, 0.0, 0.0],
    "LeftToeBase": [0.0, 0.0, 0.0],

    # right leg
    "RightUpLeg":   [0.0, 0.0, 0.0],
    "RightLeg":     [0.0, 5.0, 0.0],  # knee slight bend (Y only)
    "RightFoot":    [0.0, 0.0, 0.0],
    "RightToeBase": [0.0, 0.0, 0.0],
}

def _ensure_pose_mode(obj):
    bpy.context.view_layer.objects.active = obj
    if obj.mode != 'POSE':
        bpy.ops.object.mode_set(mode='POSE')

def reset_rotations(arm_obj, bones):
    pb = arm_obj.pose.bones
    for name in bones:
        p = pb.get(name)
        if not p: 
            print(f"[WARN] Bone not found, skip reset: {name}")
            continue
        p.rotation_mode = 'XYZ'
        p.rotation_euler = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()

def apply_pose(arm_obj, pose_deg):
    pb = arm_obj.pose.bones
    for name, eul_deg in pose_deg.items():
        p = pb.get(name)
        if not p:
            print(f"[WARN] Bone not found, skip pose: {name}")
            continue
        p.rotation_mode = 'XYZ'
        rx, ry, rz = (math.radians(eul_deg[0]),
                      math.radians(eul_deg[1]),
                      math.radians(eul_deg[2]))
        if name in HINGE_Y_ONLY:
            # Respect your Limit Rotation (X/Z locked, Y free)
            cur = list(p.rotation_euler)
            cur[1] = ry
            p.rotation_euler = tuple(cur)
        else:
            p.rotation_euler = (rx, ry, rz)
    bpy.context.view_layer.update()

def main():
    arm = bpy.data.objects.get(ARMATURE_NAME)
    if not arm or arm.type != 'ARMATURE':
        raise RuntimeError(f"Armature '{ARMATURE_NAME}' not found or not an armature")
    _ensure_pose_mode(arm)

    # Only touch the bones we care about
    reset_rotations(arm, BONES_ORDER)
    apply_pose(arm, POSE_DEGREES)

if __name__ == "__main__":
    main()
