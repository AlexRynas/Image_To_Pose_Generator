# FK applier with sign-locked semantics
# Blender 4.2+, local XYZ Euler only. No fingers, no locations/scales.

import bpy, math

ARMATURE_NAME = "Armature"

# Bones to pose
BONES_ORDER = [
    "Hips","Spine","Spine1","Spine2","Spine3","Neck","Neck1","Head",
    "LeftShoulder","LeftArm","LeftForeArm","LeftHand",
    "RightShoulder","RightArm","RightForeArm","RightHand",
    "LeftUpLeg","LeftLeg","LeftFoot","LeftToeBase",
    "RightUpLeg","RightLeg","RightFoot","RightToeBase",
]

# Optional safety toggles
SWAP_LR = False            # flip L<->R if the image was mirrored

# Auto-detect hinge axis from Limit Rotation (falls back to static sets below)
AUTO_HINGE = True

# Static hinge fallback (used if AUTO_HINGE=False or detection is inconclusive)
HINGE_Y_ONLY = {"LeftForeArm","RightForeArm","LeftLeg","RightLeg"}

# ---- FILL THIS ONLY ----
POSE_DEGREES = {
    # pelvis / spine (X=left, Y=up, Z=right)
    "Hips":   [0.0, 0.0, 0.0],
    "Spine":  [0.0, 0.0, 0.0],
    "Spine1": [0.0, 0.0, 0.0],
    "Spine2": [0.0, 0.0, 0.0],
    "Spine3": [0.0, 0.0, 0.0],

    # neck / head (X=left, Y=up, Z=right) — e.g. down-left => X>0, Y<0
    "Neck":   [0.0, 0.0, 0.0],
    "Neck1":  [0.0, 0.0, 0.0],
    "Head":   [0.0, 0.0, 0.0],

    # left arm
    # LeftShoulder: X=backward, Y=backward, Z=up
    # LeftArm:      X=forward,  Y=backward, Z=up
    # LeftForeArm:  Y=unclenches (hinge) — write Y only
    # LeftHand:     X=right, Y=up, Z=backward
    "LeftShoulder":[0.0, 0.0, 0.0],
    "LeftArm":[0.0, 0.0, 0.0],
    "LeftForeArm":[0.0, 0.0, 0.0],
    "LeftHand":[0.0, 0.0, 0.0],

    # right arm
    # RightShoulder:X=backward, Y=backward, Z=up
    # RightArm:     X=forward, Y=backward, Z=up
    # RightForeArm: Y=unclenches (hinge) — write Y only
    # RightHand:    X=left, Y=up, Z=backward
    "RightShoulder":[0.0, 0.0, 0.0],
    "RightArm":[0.0, 0.0, 0.0],
    "RightForeArm":[0.0, 0.0, 0.0],
    "RightHand":[0.0, 0.0, 0.0],

    # legs/feet
    # LeftUpLeg:   X=right, Y=forward, Z=right
    # LeftLeg:     Y=forward (hinge)
    # LeftFoot:    X=right, Y=up, Z=right
    # LeftToeBase: X=right, Y=up, Z=right
    "LeftUpLeg":[0.0, 0.0, 0.0],
    "LeftLeg":[0.0, 0.0, 0.0],
    "LeftFoot":[0.0, 0.0, 0.0],
    "LeftToeBase":[0.0, 0.0, 0.0],

    # RightUpLeg:  X=left, Y=forward, Z=left
    # RightLeg:    Y=forward (hinge)
    # RightFoot:   X=left, Y=up, Z=left
    # RightToeBase:X=left, Y=up, Z=left
    "RightUpLeg":[0.0, 0.0, 0.0],
    "RightLeg":[0.0, 0.0, 0.0],
    "RightFoot":[0.0, 0.0, 0.0],
    "RightToeBase":[0.0, 0.0, 0.0],
}
# ---- END FILL ----

# Optional L/R swap mapping (used if SWAP_LR=True)
LR_MAP = {
    "LeftShoulder":"RightShoulder","RightShoulder":"LeftShoulder",
    "LeftArm":"RightArm","RightArm":"LeftArm",
    "LeftForeArm":"RightForeArm","RightForeArm":"LeftForeArm",
    "LeftHand":"RightHand","RightHand":"LeftHand",
    "LeftUpLeg":"RightUpLeg","RightUpLeg":"LeftUpLeg",
    "LeftLeg":"RightLeg","RightLeg":"LeftLeg",
    "LeftFoot":"RightFoot","RightFoot":"LeftFoot",
    "LeftToeBase":"RightToeBase","RightToeBase":"LeftToeBase",
}

def _ensure_pose_mode(obj):
    bpy.context.view_layer.objects.active = obj
    if obj.mode != 'POSE':
        bpy.ops.object.mode_set(mode='POSE')

def _swap_lr_key(name):
    return LR_MAP.get(name, name) if SWAP_LR else name

def _auto_hinge_axis(pb):
    if not AUTO_HINGE:
        return None
    lr = next((c for c in pb.constraints if c.type=='LIMIT_ROTATION' and not c.mute), None)
    if not lr:
        return None
    def axis_allowed(a):
        use = getattr(lr, f"use_limit_{a}")
        if not use:
            return True
        mn = getattr(lr, f"min_{a}")
        mx = getattr(lr, f"max_{a}")
        # Locked if min==max==0
        return not (abs(mn) < 1e-6 and abs(mx) < 1e-6)
    allowed = [axis_allowed('x'), axis_allowed('y'), axis_allowed('z')]
    if sum(allowed) == 1:
        return ['x','y','z'][allowed.index(True)]
    return None

def _set_euler(arm_obj, bone_name, rx, ry, rz):
    pb = arm_obj.pose.bones.get(bone_name)
    if not pb:
        print(f"[WARN] Missing bone: {bone_name}")
        return
    pb.rotation_mode = 'XYZ'
    # Hinge handling
    hinge = _auto_hinge_axis(pb)
    if hinge:
        cur = list(pb.rotation_euler)
        if hinge == 'x': cur[0] = rx
        if hinge == 'y': cur[1] = ry
        if hinge == 'z': cur[2] = rz
        pb.rotation_euler = tuple(cur)
    elif bone_name in HINGE_Y_ONLY:
        cur = list(pb.rotation_euler)
        cur[1] = ry
        pb.rotation_euler = tuple(cur)
    else:
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

def main():
    arm = bpy.data.objects.get(ARMATURE_NAME)
    if not arm or arm.type != 'ARMATURE':
        raise RuntimeError(f"Armature '{ARMATURE_NAME}' not found")
    _ensure_pose_mode(arm)
    reset_rotations(arm)
    apply_pose(arm, POSE_DEGREES)
    print("[apply_pose_template_v3] pose applied.")

if __name__ == "__main__":
    main()
