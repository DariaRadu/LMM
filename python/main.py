from pathlib import Path

import pathlib
from torch.utils.data.dataloader import DataLoader

import numpy as np

from data import BVHDataset
from databases import Joint, Feature
import utils

from decompressor import Decompressor

import sys

basedir = str(pathlib.Path(__file__).parent.absolute())
data_dir = basedir + "/ubisoft-laforge-animation-dataset/output/BVH"
databases_data_dir = basedir + "/databases"

# Training variables
batch_size = 32
sequence_length = 200
n_batches = 10000
latent_size = 512
learning_rate = 0.0001

animation_db = []
animation_db_train = []
matching_db = []
matching_db_train = []
input_names = []

def read_animation_db():
    with open(databases_data_dir + '/202012011858AnimationDB.txt') as file:
        animation_db_raw = file.read().splitlines()

    # Formats the animation database
    for i in range(len(animation_db_raw)):
        animation_db_raw[i] = animation_db_raw[i].split(';')
    for line in animation_db_raw:
        for i in range(len(line)):
            line[i] = line[i].split(',')
            if line[i][0][0] == '(': line[i][0] = line[i][0][1:]
            if line[i][-1][-1] == ')': line[i][-1] = line[i][-1][:-1]

    for i in range(0, len(animation_db_raw), 22):  # The current skeleton structure only has 22 joints
        pose = []
        pose_raw = []
        for j in range(i, i + 22):
            if i == 0:
                joint_name = animation_db_raw[j][0][0].replace('input', '')
                input_names.append(joint_name)
            joint = Joint(animation_db_raw[j])
            pose.append(joint)
            pose_raw.append(joint.localPosition)
        animation_db.append(pose)
        pose_raw = np.array(pose_raw).flatten()
        animation_db_train.append(pose_raw)

def read_matching_db():
    with open(databases_data_dir + '/202012041850MatchingDB.txt') as file:
        matching_db_raw = file.read().splitlines()

    # Formats the matching database
    for i in range(len(matching_db_raw)):
        matching_db_raw[i] = matching_db_raw[i].split(';')

    for line in matching_db_raw:
        feature_raw = []
        for i in range(len(line)):
            line[i] = line[i].split(',')
            if line[i][0][0] == '(': line[i][0] = line[i][0][1:]
            if line[i][-1][-1] == ')': line[i][-1] = line[i][-1][:-1]

        feature_vector = Feature(line)
        feature_raw.append(feature_vector.futureTrajectories)
        feature_raw = np.array(feature_raw).flatten()
        matching_db_train.append(feature_raw)
        matching_db.append(feature_vector)


output_names = ["Hips",
                "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToe",
                "RightUpLeg", "RightLeg", "RightFoot", "RightToe",
                "Spine", "Spine1", "Spine2", "Neck", "Head",
                "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                "RightShoulder", "RightArm", "RightForeArm", "RightHand"]

# Load data for Unity
data = BVHDataset(data_dir, sequence_length, "train_data", output_names, output_names)
dataLoader = DataLoader(data, batch_size=batch_size)


# Reads the animation database
read_animation_db()
read_matching_db()

# Prepare for training
model_name = "./models/model" + "_bs" + str(batch_size) + "_sl" + str(sequence_length) + "_nb" + str(n_batches) + \
             "_ls" + str(latent_size) + "_lr" + str(learning_rate) + ".pt"

# these are lists - need to convert to dataset
train_dataloader = DataLoader(animation_db_train[:-100], batch_size=batch_size)
val_dataloader = DataLoader(animation_db_train[-100:], batch_size=batch_size)
matching_db_train_dataloader = DataLoader(matching_db_train[:-100], batch_size=batch_size)
matching_db_val_dataloader = DataLoader(matching_db_train[-100:], batch_size=batch_size)

decompressor = Decompressor(input_size=len(input_names) * 3, output_size=len(output_names) * 3,
                      latent_size=latent_size, learning_rate=learning_rate)

# if Path(model_name).exists():
#     decompressor.load(model_name)
# else:
print("Training decompressor", decompressor)
decompressor.do_train(train_dataloader, n_batches, val_dataloader, matching_db_train_dataloader, matching_db_val_dataloader)
decompressor.save(model_name)


# Send data to file that will be read in Unity
original_stdout = sys.stdout
with open('output/input.pose', 'w') as f:
    sys.stdout = f
    utils.print_sequences(data.input, output_names, 999999, positions=True, frames=True, prefix="input", flush=False)
    sys.stdout = original_stdout
