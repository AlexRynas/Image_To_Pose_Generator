# apply_pose_sanity.py — tiny sanity check for your "Armature"
# Blender 4.x. Sets modest rotations on major bones; no IK; no fingers.

import bpy, math

ARMATURE_NAME = "Armature"

# Bones we’ll touch (matches your report; no fingers)
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

# Your rig locks X/Z on elbows & knees: write Y only for these.
HINGE_Y_ONLY = {"LeftForeArm", "RightForeArm", "LeftLeg", "RightLeg"}

# A mild, symmetric test pose in local XYZ degrees.
# Intent: tiny spine bend, slight head turn, small elbow/knee flex.
POSE_DEGREES = {
    # pelvis & spine
    "Hips":        [0.0, 0.0, 0.0],
    "Spine":       [2.0, 0.0, 0.0],
    "Spine1":      [2.0, 0.0, 0.0],
    "Spine2":      [2.0, 0.0, 0.0],
    "Spine3":      [1.0, 0.0, 0.0],
    "Neck":        [0.0, 2.0, 0.0],
    "Neck1":       [0.0, 2.0, 0.0],
    "Head":        [0.0, 5.0, 0.0],

    # left arm (minimal shoulder change; clear elbow bend)
    "LeftShoulder":[0.0, 0.0, 0.0],
    "LeftArm":     [0.0, 0.0, 0.0],
    "LeftForeArm": [0.0, 20.0, 0.0],  # elbow flex ~20° (Y only)
    "LeftHand":    [0.0, 0.0, 0.0],

    # right arm
    "RightShoulder":[0.0, 0.0, 0.0],
    "RightArm":     [0.0, 0.0, 0.0],
    "RightForeArm": [0.0, 20.0, 0.0], # elbow flex ~20° (Y only)
    "RightHand":    [0.0, 0.0, 0.0],

    # left leg (light knee bend)
    "LeftUpLeg":   [0.0, 0.0, 0.0],
    "LeftLeg":     [0.0, 5.0, 0.0],   # knee ~5° (Y only)
    "LeftFoot":    [0.0, 0.0, 0.0],
    "LeftToeBase": [0.0, 0.0, 0.0],

    # right leg
    "RightUpLeg":   [0.0, 0.0, 0.0],
    "RightLeg":     [0.0, 5.0, 0.0],  # knee ~5° (Y only)
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

    reset_rotations(arm, BONES_ORDER)
    apply_pose(arm, POSE_DEGREES)
    print("[Sanity Pose] Applied small test rotations to major bones.")

if __name__ == "__main__":
    main()
