# THIS CODE IS OUTSOURCED

from pathlib import Path
import datetime
import os
import time

import numpy as np
from torch.utils.tensorboard import SummaryWriter

import transformations as transform

def print_frame(frame, joint_names, positions=True, frames=False, quaternions=False, prefix ="", flush=True):
    pos_l = 3 if positions else 0  # pos vector3
    quat_l = 4 if quaternions else 0 # rot quaternion
    frame_l = 6 if frames else 0  # rot frame

    n_joint = (len(frame)) // (pos_l + frame_l + quat_l)

    for j_id in range(n_joint):
        idx = j_id * (pos_l + frame_l + quat_l)
        p = frame[idx:idx + pos_l] if positions else [0, 0, 0]
        q = frame[idx + pos_l:idx + pos_l + quat_l] if quaternions else [1, 0, 0, 0]
        f = frame[idx + pos_l + quat_l:idx + pos_l + quat_l + frame_l] if frames else [1, 0, 0, 0, 1, 0]

        print("G", "original", prefix + joint_names[j_id], p[0], p[1], p[2], q[1], q[2], q[3], q[0], f[0], f[1], f[2], f[3], f[4], f[5], flush=flush)
#        print("G", "original", prefix + str(j_id), p[0], p[1], p[2], q[1], q[2], q[3], q[0], f[0], f[1], f[2], f[3], f[4], f[5], flush=flush)


def print_sequence(sequence: np.ndarray, joint_names, frame_rate: int = 15, positions=True, frames=False, quaternions=False, prefix = "", flush=True):
    frame_time = (1 / frame_rate) if frame_rate is not None else 0
    for frame in sequence:
        start = time.clock()

        print_frame(frame, joint_names, positions, frames, quaternions, prefix=prefix, flush=flush)

        elapsed_time = time.clock() - start
        time_left = frame_time - elapsed_time
        if time_left > 0:
            time.sleep(time_left)
    time.sleep(1)

def print_sequences(sequences: np.ndarray, joint_names, frame_rate: int = 15, positions=True, frames=False, quaternions=False, prefix ="", flush=True):
    """
    Prints the sequences, one after another, for unity visualization

    :param sequences: (n_sequences, n_frames, frame_size) A batch of sequences
    :param frame_rate: Desired frame rate of prints
    :return:
    """
    for sequence in sequences:
        print_sequence(sequence, joint_names, frame_rate,  positions, frames, quaternions, prefix, flush)



def get_writers(name):
    tensorboard_dir = os.environ.get('TENSORBOARD_DIR') or 'tensorboard'
    revision = os.environ.get("REVISION") or datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    message = os.environ.get('MESSAGE')

    train_writer = SummaryWriter(tensorboard_dir + '/%s/%s/train/%s' % (name, revision, message))
    val_writer = SummaryWriter(tensorboard_dir + '/%s/%s/val/%s' % (name, revision, message))
    return train_writer, val_writer
