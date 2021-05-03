# THIS CODE IS OUTSOURCED

from bvh import Bvh
import numpy as np
import transformations as transform
import math


class BVHAnimation:
    ## missing support to channel order, so that it can automatically convert any euler angle order
    def __init__(self, bvh_path):
        with open(bvh_path) as f:
            mocap = Bvh(f.read())

        self.joints_names = mocap.get_joints_names()
        self.n_joints = len(self.joints_names)
        self.joints_parent_ids = np.array([mocap.joint_parent_index(j_name) for j_name in self.joints_names])
        self.joints_channel_ids = np.array([mocap.get_joint_channels_index(j_name) for j_name in self.joints_names])
        self.joints_offsets = np.array([mocap.joint_offset(j_name) for j_name in self.joints_names])
        self.frames = np.array([np.array(xi, float) for xi in mocap.frames])
        self.frames[:, 3:] *= np.pi / 180  # rotation angles from degrees to radians
        self.frame_time = mocap.frame_time
        self.n_frames = mocap.nframes
        self.joint_id_from_name = dict(zip(self.joints_names, range(len(self.joints_names))))

    def get_pose(self, frame, local_space=False):
        pose = [None] * len(self.joints_names)

        for id in range(self.n_joints):
            channel = self.joints_channel_ids[id]
            parent_id = self.joints_parent_ids[id]

            if parent_id < 0:  # is root
                mat = transform.euler_matrix(-self.frames[frame, channel + 5], # hard coded right to left handed CS (swap z axis)
                                             -self.frames[frame, channel + 4], # hard coded right to left handed CS (swap z axis)
                                             self.frames[frame, channel + 3], 'sxyz')
                mat[0:3, 3] = self.frames[frame, channel:channel + 3] / 7 #translation
                mat[2, 3] = -mat[2, 3] # hard coded right to left handed CS (swap z axis)
                pose[id] = mat

            else:
                mat = transform.euler_matrix(-self.frames[frame, channel + 2], # hard coded right to left handed CS (swap z axis)
                                             -self.frames[frame, channel + 1], # hard coded right to left handed CS (swap z axis)
                                             self.frames[frame, channel], 'sxyz')
                mat[0:3, 3] = self.joints_offsets[id] / 7 # translation
                mat[2, 3] = -mat[2, 3] # hard coded right to left handed CS (swap z axis)
                pose[id] = pose[id] = mat if local_space else np.dot(pose[parent_id], mat)

        return pose

    def as_animation_table(self, subsamplestep=1, joints_names = None, root_space : bool = True,
                           positions : bool = False, quaternions : bool = False, frames : bool = False):
        # poses is a list (frames) of lists (joints), with a 4x4 transformation matrix in each entry
        # output is a pose per row:
        # root quaternion - root positions - joint positions ...
        cols = (3 if positions else 0) + (4 if quaternions else 0) + (6 if frames else 0)
        assert positions or quaternions or frames is not False, print("nothing selected for the table")
        if root_space:
            poses = self.get_animation_root_space(subsamplestep)
        else:
            poses = self.get_animation_minus_root_pos(subsamplestep)

        anim_vec = np.zeros((len(poses), len(joints_names if joints_names is not None else poses[0]) * cols))
        ## select joints
        joints_idx = [self.joint_id_from_name[name] for name in joints_names] if joints_names is not None else range(len(poses[0]))

        for p in range(len(poses)):
            col = 0
            for j in joints_idx:
                j_mat = poses[p][j]
                if positions:
                    anim_vec[p, col:col + 3] = transform.translation_from_matrix(j_mat)
                    col += 3
                if quaternions:
                    anim_vec[p, col:col + 4] = transform.quaternion_from_matrix(j_mat, isprecise=True)
                    col += 4
                if frames:
                    anim_vec[p, col:col + 6] = np.concatenate([j_mat[0:3, 0], j_mat[0:3, 1]])
                    col += 6

        return anim_vec

    # def as_joint_position(self, subsamplestep=1, joints_names = None):
    #     # poses is a list (frames) of lists (joints), with a 4x4 transformation matrix in each entry
    #     # output is a pose per row:
    #     # root quaternion - root positions - joint positions ...
    #
    #     poses = self.get_animation_root_space(subsamplestep)
    #     anim_vec = np.zeros((len(poses), len(poses[0]) * 3))
    #     ## select joints
    #     joints_idx = [self.joint_id_from_name[name] for name in joints_names] if joints_names is not None else range(len(poses[0]))
    #
    #     for p in range(len(poses)):
    #         # we assume that the first entry is the root
    #         col = 0
    #         for j in joints_idx:
    #             j_mat = poses[p][j]
    #             anim_vec[p, col:col + 3] = transform.translation_from_matrix(j_mat)
    #             col += 3
    #
    #     return anim_vec

    def get_animation_global(self, subsamplestep=1):
        nframes = math.floor((self.n_frames + subsamplestep - 1) / subsamplestep)
        poses = [None] * nframes
        current = 0
        for frame in range(0, self.n_frames, subsamplestep):
            poses[current] = self.get_pose(frame)
            current += 1

        return poses

    def get_animation_local(self, subsamplestep=1):
        nframes = math.floor((self.n_frames + subsamplestep - 1) / subsamplestep)
        poses = [None] * nframes
        current = 0
        for frame in range(0, self.n_frames, subsamplestep):
            poses[current] = self.get_pose(frame, True)
            current += 1

        return poses

    def get_animation_root_space(self, subsamplestep=1):
        nframes = math.floor((self.n_frames + subsamplestep - 1) / subsamplestep)
        poses = [None] * nframes
        current = 0
        for frame in range(0, self.n_frames, subsamplestep):
            poses[current] = self.get_pose(frame)
            root_inv = transform.inverse_matrix(poses[current][0])
            for j_id in range(1, self.n_joints):
                poses[current][j_id] = np.dot(root_inv, poses[current][j_id])
            current += 1

        return poses

    def get_animation_minus_root_pos(self, subsamplestep=1):
        nframes = math.floor((self.n_frames + subsamplestep - 1) / subsamplestep)
        poses = [None] * nframes
        current = 0
        for frame in range(0, self.n_frames, subsamplestep):
            poses[current] = self.get_pose(frame)
            root_pos = poses[current][0][0:3, 3]
            for j_id in range(1, self.n_joints):
                poses[current][j_id][0:3, 3] -= root_pos
            current += 1

        return poses

import utils
import pathlib
import glob
import tqdm as tqdm
if __name__ == '__main__':
    input_names = None#["Hips", "LeftFoot", "RightFoot", "Head", "LeftHand", "RightHand"]

    bvh_dir = str(pathlib.Path(__file__).parent.absolute()) + "/ubisoft-laforge-animation-dataset/output/BVH"
    bvh_files = glob.glob(bvh_dir + "/*.bvh")

    assert len(bvh_files) > 0, "No .bvh files found in " + bvh_files
    print(bvh_files)

    positions = [BVHAnimation(file).as_animation_table(4, input_names, positions=True) for file in tqdm.tqdm(bvh_files, desc="Loading bvh files. This will only happen once. Be patient")]

    utils.print_sequences(positions)

