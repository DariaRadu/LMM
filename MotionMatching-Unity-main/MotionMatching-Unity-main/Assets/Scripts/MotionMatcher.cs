using System;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MotionMatcher : MonoBehaviour
{
    public string textFilePathAndName;
    public int frameRate = 15;
    public bool debug = true;
    List<string> animationLines = new List<string>();
    public List<Feature> MatchingDB;
    List<Pose> AnimationDB; 
    int currentFrame = 0;
    private Trajectory trajectory;
    private string delimiter = ";";

    void ReadString(string path)
    {
        StreamReader reader = new StreamReader(path);
        animationLines.Clear();
        currentFrame = 0;
        while (!reader.EndOfStream)
        {
            string frame = reader.ReadLine();
            animationLines.Add(frame);
        }
        reader.Close();
    }

    void Start()
    {
        ReadString(textFilePathAndName);

        trajectory = gameObject.GetComponent<Trajectory>();
        AnimationDB = CalculateAnimationDB();
        MatchingDB = CalculateMatchingDB();
        WriteAnimationDBToFile();
        WriteMathingDBToFile();
    }

    List<Pose> CalculateAnimationDB()
    {
        List<Pose> animationDB = new List<Pose>{ };
        Vector3 initialOffset = Vector3.zero;
        for (int i = 0; i< animationLines.Count; i=i+22)
        {
            Pose pose = new Pose();
            List<MotionJointData> frame = new List<MotionJointData> { };
            List<MotionJointData> prevFrame = new List<MotionJointData> { };
            for (int index = i; index < i + 22; index ++)
            {
                string[] info = animationLines[index].Split(' ');
                string jointName = info[2];
                MotionJointData joint = new MotionJointData();
                joint.name = jointName;
                Vector3 position = new Vector3(parsefloat(info[3]), parsefloat(info[4]), parsefloat(info[5]));
                joint.localPosition = position;
                Vector3 j = new Vector3(parsefloat(info[10]), parsefloat(info[11]), parsefloat(info[12]));
                Vector3 k = new Vector3(parsefloat(info[13]), parsefloat(info[14]), parsefloat(info[15]));
                Vector3 l = Vector3.Cross(j, k);
                Quaternion rotation = Quaternion.LookRotation(l, k);
                if (jointName == "inputHips")
                {
                    if (animationDB.Count == 0) initialOffset = joint.localPosition;

                    rotation = Quaternion.AngleAxis(180, Vector3.up) * Quaternion.LookRotation(l, k);
                    joint.localPosition -= initialOffset;
                    if (animationDB.Count > 0)
                    {
                        Vector3 prevLocalPosition = animationDB[animationDB.Count - 1].joints[0].localPosition;
                        Vector3 currentLocalPosition = joint.localPosition;
                        Vector3 offset = currentLocalPosition - prevLocalPosition;
                        joint.localPosition = -joint.localPosition;
                    }
                }
                joint.localRotation = rotation;
                joint.index = index;

                if (animationDB.Count > 0)
                {
                    Pose lastPose = animationDB[animationDB.Count - 1];
                    MotionJointData lastJointPose = lastPose.joints.Find(lastJoint => lastJoint.name == jointName);
                    Vector3 jointVelocity = (joint.localPosition - lastJointPose.localPosition) / Time.deltaTime;
                    joint.velocity = jointVelocity;

                } else
                {
                    joint.velocity = Vector3.zero;
                }

                frame.Add(joint);
            }
            pose.joints = frame;
            prevFrame = frame;

            animationDB.Add(pose);
        }

        Debug.Log("Animation Database calculated.");
        return animationDB;
    }

    List<Feature> CalculateMatchingDB()
    {
        List<Feature> matchingDB = new List<Feature> { };
        for (int i = 0; i <= AnimationDB.Count - 1; i++)
        {
            Pose currentPose = AnimationDB[i];
            Feature featureVector = new Feature();
            MotionTrajectoryData trajectoryData = new MotionTrajectoryData();
            foreach (MotionJointData joint in currentPose.joints)
            {
                if (joint.name == "inputHips")
                {
                    featureVector.hipVelocity = joint.velocity;
                    trajectoryData.position = Vector3.zero;
                    trajectoryData.velocity = new Vector3(joint.velocity.x, 0, joint.velocity.z);
                    featureVector.trajectory = trajectoryData;
                    featureVector.futureTrajectories = new List<MotionTrajectoryData> { };
                    for (int j = 5; j <= 15; j += 5)
                    {
                        if (i + j >= AnimationDB.Count) continue;
                        MotionTrajectoryData futureTrajectory = new MotionTrajectoryData();
                        MotionJointData hipJoint = AnimationDB[i + j].joints[0];
                        float futureHipJointX = hipJoint.localPosition.x - joint.localPosition.x;
                        float futureHipJointZ = hipJoint.localPosition.z - joint.localPosition.z;
                        futureTrajectory.position = new Vector3(futureHipJointX, 0, futureHipJointZ);
                        futureTrajectory.velocity = new Vector3(hipJoint.velocity.x, 0, hipJoint.velocity.z);
                        featureVector.futureTrajectories.Add(futureTrajectory);
                    }
                }

                if (joint.name == "inputLeftFoot")
                {
                    featureVector.leftFootPosition = joint.localPosition;
                    featureVector.leftFootVelocity = joint.velocity;
                }

                if (joint.name == "inputRightFoot")
                {
                    featureVector.rightFootPosition = joint.localPosition;
                    featureVector.rightFootVelocity = joint.velocity;
                }
            }

            matchingDB.Add(featureVector);
        }
        Debug.Log("Matching Database calculated.");
        return matchingDB;
    }

    float parsefloat(string s)
    {
        return float.Parse(s, CultureInfo.InvariantCulture.NumberFormat);
    }

    public int CalculateBestCandidateIndex(Feature queryVector)
    {
        float bestCost = 10000000000.0f;
        int bestIndex = -1;
        for (int i = 0; i < MatchingDB.Count - 60; i++)
        {
            Feature candidate = MatchingDB[i];

            float thisCost = ComputeCost(queryVector, candidate);
            
            if (thisCost < bestCost)
            {
                
                bestCost = thisCost;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public void PlayAnim(int index)
    {
        Transform skeleton = GameObject.Find("inputHips").transform;
        Pose currentPose = AnimationDB[index];
        for (int i = 0; i < currentPose.joints.Count; i++)
        {
            MotionJointData joint = currentPose.joints[i];
            if (joint.name == "inputHips")
            {
                Transform jointTransform = skeleton;

                Vector3 prevLocalPosition = i == 0 ? Vector3.zero : currentPose.joints[i - 1].localPosition;
                jointTransform.transform.localPosition -= prevLocalPosition;
                Vector3 jointForward = Quaternion.AngleAxis(-90, Vector3.up) * jointTransform.forward;
                Debug.DrawLine(jointTransform.position, jointTransform.position + 10 * jointForward, Color.red, 1.0f);
                jointTransform.transform.localRotation = joint.localRotation;
            }
            else
            {
                Transform jointTransform = skeleton.Find(joint.name);
                UpdateRigidBody(jointTransform, joint);
            }
        }
    }

    private void UpdateRigidBody(Transform jointTransform, MotionJointData joint)
    {
        jointTransform.transform.localPosition = joint.localPosition;
        jointTransform.transform.localRotation = joint.localRotation;
    }

    private float ComputeCost(Feature queryVector, Feature candidate)
    {
        float cost = ComputeCurrentCost(queryVector, candidate);
        float responsitivity = 0.8f;
        cost += responsitivity * ComputeFutureCost(queryVector, candidate);
        return cost;
    }

    private float ComputeCurrentCost(Feature current, Feature candidate)
    {
        float cost = 0.0f;

        // Move the trajectory from character space to the current world space
        // since current trajectory is saved in world space
        // However, since candidate trajectory always starts at 0, the distance between the current
        // trajectories will be 0 (so no need to calculate it)
        cost += Vector3.Distance(current.trajectory.velocity, candidate.trajectory.velocity) *
            Vector3.Distance(current.trajectory.velocity, candidate.trajectory.velocity);
        cost += Vector3.Distance(current.leftFootPosition, candidate.leftFootPosition) *
            Vector3.Distance(current.leftFootPosition, candidate.leftFootPosition);
        cost += Vector3.Distance(current.rightFootPosition, candidate.rightFootPosition) *
            Vector3.Distance(current.rightFootPosition, candidate.rightFootPosition);
        cost += Vector3.Distance(current.hipVelocity, candidate.hipVelocity) *
            Vector3.Distance(current.hipVelocity, candidate.hipVelocity);
        return cost;
    }

    private float ComputeFutureCost(Feature current, Feature candidate)
    {
        float cost = 0.0f;
        for (int i = 0; i < candidate.futureTrajectories.Count; i++)
        {
            // Move the future trajectories of the candidate vector from character space to world space
            Vector3 relativeTrajectoryPosition = candidate.futureTrajectories[i].position + current.trajectory.position;
            cost += Vector3.Distance(current.futureTrajectories[i].position, relativeTrajectoryPosition) *
                Vector3.Distance(current.futureTrajectories[i].position, relativeTrajectoryPosition);
        }
        return cost;
    }

    private void WriteAnimationDBToFile()
    {
        string date = DateTime.Now.ToString("yyyyMMddHHmm");
        string file = "../python/databases/" + date +"AnimationDB.txt";
        StreamWriter writer = new StreamWriter(file, true);

        foreach (Pose pose in AnimationDB)
        {
            foreach (MotionJointData joint in pose.joints)
            {
                string line = joint.name + ";" + joint.localPosition + ";" + joint.localRotation + ";" + joint.velocity;
                writer.WriteLine(line);
            }
        }
        Debug.Log("Writing Animation Database to file finished.");
        writer.Close();
    }

    private void WriteMathingDBToFile()
    {
        string date = DateTime.Now.ToString("yyyyMMddHHmm");
        string file = "../python/databases/" + date + "MatchingDB.txt";
        StreamWriter writer = new StreamWriter(file, true);

        foreach (Feature feature in MatchingDB)
        {
            string line = "";
            line += feature.hipVelocity + delimiter + feature.leftFootPosition + delimiter + feature.leftFootVelocity
                + delimiter + feature.rightFootPosition + delimiter + feature.rightFootVelocity + delimiter;

            line += feature.trajectory.position + delimiter + feature.trajectory.velocity;

            foreach (MotionTrajectoryData futureTrajectory in feature.futureTrajectories)
            {
                line += delimiter + futureTrajectory.position + delimiter + futureTrajectory.velocity;
            }

            writer.WriteLine(line);
        }
        Debug.Log("Writing Matching Database to file finished.");
        writer.Close();
    }

    private void OnDrawGizmos()
    {
        if (!debug) return;
        for (int i = 0; i < 40; i++)
        {
            Feature feature = MatchingDB[i];
            Vector3 featurePosition = feature.trajectory.position;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(featurePosition, 1);
        }
    }
}
