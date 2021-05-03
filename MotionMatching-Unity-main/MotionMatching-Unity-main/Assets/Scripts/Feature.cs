using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Feature
{
    public List<MotionTrajectoryData> futureTrajectories;
    public MotionTrajectoryData trajectory;
    public Vector3 rightFootPosition;
    public Vector3 leftFootPosition;
    public Vector3 rightFootVelocity;
    public Vector3 leftFootVelocity;
    public Vector3 hipVelocity;
}
