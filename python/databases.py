import numpy as np

class Joint:
    def __init__(self, jointData):
        self.name = jointData[0][0]
        self.localPosition = np.array(jointData[1]).astype(np.float)
        self.localRotation = np.array(jointData[2]).astype(np.float)
        self.velocity = np.array(jointData[3]).astype(np.float)

class Feature:
    def __init__(self, featureData):
        self.hipVelocity = np.array(featureData[0]).astype(np.float)
        self.leftFootPosition = np.array(featureData[1]).astype(np.float)
        self.leftFootVelocity = np.array(featureData[2]).astype(np.float)
        self.rightFootPosition = np.array(featureData[3]).astype(np.float)
        self.rightFootVelocity = np.array(featureData[4]).astype(np.float)
        self.trajectoryPosition = np.array(featureData[5]).astype(np.float)
        self.trajectoryVelocity = np.array(featureData[6]).astype(np.float)
        self.futureTrajectories = []

        for i in range (7, len(featureData) - 1):
            futureTrajectory = [np.array(featureData[i]).astype(np.float),
                                 np.array(featureData[i + 1]).astype(np.float)]
            self.futureTrajectories.append(futureTrajectory);