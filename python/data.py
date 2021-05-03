# THIS CODE IS OUTSOURCED

from pathlib import Path
import glob
import os
import random

import numpy as np
import tqdm as tqdm
from torch.utils.data.dataset import IterableDataset

from bvh_animation import BVHAnimation

import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D

import utils

class BVHDataset(IterableDataset):

    def __init__(self, data_dir: str, sequence_length: int, file_name: str, input_names, output_names):
        assert not data_dir.endswith("/"), "data_dir should not end with a /"
        self.sequence_length = sequence_length
        cache_file = data_dir + '/' + file_name + 'cache.npz'
        subsample_factor = 4

        if os.path.exists(cache_file):
            with np.load(cache_file, allow_pickle=True) as data:
                self.input = data["input"]
                self.output = data["output"]

        else:
            bvh_files = glob.glob(data_dir + "/*.bvh")
            assert len(bvh_files) > 0, "No .bvh files found in " + data_dir
            self.input = [BVHAnimation(file).as_animation_table(subsample_factor, input_names, root_space=True, positions=True, frames=True)
                               for file in tqdm.tqdm(bvh_files, desc="Loading bvh files. This will only happen once. Be patient")]
            self.output = [BVHAnimation(file).as_animation_table(subsample_factor, output_names, root_space=True, positions=True, frames=True)
                               for file in tqdm.tqdm(bvh_files, desc="Loading bvh files. This will only happen once. Be patient")]
            ## TODO select what is input and what is output data
            np.savez_compressed(cache_file, input=self.input, output=self.output)

    def __iter__(self):
        while True:
            n_animations = len(self.input)
            for idx in random.sample(range(n_animations), n_animations):
                yield self.random_sequence(self.input[idx], self.output[idx])

    def random_sequence(self, animation_in, animation_out):
        start_idx = random.randint(0, len(animation_in) - self.sequence_length)
        return animation_in[start_idx:start_idx + self.sequence_length], animation_out[start_idx:start_idx + self.sequence_length]

    @staticmethod
    def plot_frame(frame):
        """
        plot a single frame from the dataset

        :param frame: (97, ) (
        :return:
        """

        def set_axes_equal(ax):
            '''Make axes of 3D plot have equal scale so that spheres appear as spheres,
            cubes as cubes, etc..  This is one possible solution to Matplotlib's
            ax.set_aspect('equal') and ax.axis('equal') not working for 3D.

            Input
              ax: a matplotlib axis, e.g., as output from plt.gca().
            '''

            x_limits = ax.get_xlim3d()
            y_limits = ax.get_ylim3d()
            z_limits = ax.get_zlim3d()

            x_range = abs(x_limits[1] - x_limits[0])
            x_middle = np.mean(x_limits)
            y_range = abs(y_limits[1] - y_limits[0])
            y_middle = np.mean(y_limits)
            z_range = abs(z_limits[1] - z_limits[0])
            z_middle = np.mean(z_limits)

            # The plot bounding box is a sphere in the sense of the infinity
            # norm, hence I call half the max range the plot radius.
            plot_radius = 0.5 * max([x_range, y_range, z_range])

            ax.set_xlim3d([x_middle - plot_radius, x_middle + plot_radius])
            ax.set_ylim3d([y_middle - plot_radius, y_middle + plot_radius])
            ax.set_zlim3d([z_middle - plot_radius, z_middle + plot_radius])

        fig = plt.figure(figsize=(8, 8))
        ax: Axes3D = fig.add_subplot(111, projection='3d')

        positions = frame[7:]  # position
        xs, ys, zs = (positions[0::3], positions[1::3], positions[2::3])
        ax.scatter(xs, zs, ys)
        set_axes_equal(ax)
        plt.show()


if __name__ == '__main__':
    input_names = ["Hips", "LeftFoot", "RightFoot"]
    output_names = ["Hips", "LeftFoot", "RightFoot"]

    dataset = BVHDataset("./ubisoft-laforge-animation-dataset/output/BVH", 100, "test_data", input_names, output_names)
    for seq in tqdm.tqdm(dataset, desc="Testing dataset speed"):  # Around 40k sequences/s
        continue
