using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Trajectory : MonoBehaviour
{
    public double[] lookUpTableW = new double[] { 5.30747, 4.47228, 3.97068, 3.60702, 3.31942, 3.0801, 2.87415, 2.69263, 2.52973, 2.38146, 2.24494, 2.11803, 1.9991, 1.88684, 1.78021, 1.67835, 1.58051, 1.48607, 1.39445, 1.30515, 1.21767, 1.13154, 1.04625, 0.961279, 0.876004, 0.789671, 0.70128, 0.609381, 0.511626, 0.403527, 0.273582, 0 };
    private double stepValue = 0.03125;

    public MotionTrajectoryData Step(MotionTrajectoryData trajectoryData, float deltaTime, float goal, float timeElapsed)
    {
        if (timeElapsed > 1.0f)
        {
            timeElapsed = 1.0f;
        }

        Vector3 nextVelocity = CalculateCriticallyDampedSpring(0.1f, 50.0f, timeElapsed, 1, deltaTime);

        MotionTrajectoryData nextTrajectoryData = new MotionTrajectoryData();
        nextTrajectoryData.velocity = nextVelocity;
        float nextSpeed = nextVelocity.magnitude;

        Vector3 nextDirection = CalculateCriticallyDampedSpring(Vector3.zero, trajectoryData.desiredDirection, timeElapsed, 1, deltaTime);

        nextTrajectoryData.direction = nextDirection;
        Vector3 nextPosition = trajectoryData.position + deltaTime * nextSpeed * nextTrajectoryData.direction;

        nextTrajectoryData.position = nextPosition;

        return nextTrajectoryData;
    }

    Vector3 CalculateCriticallyDampedSpring(float value, float velocity, float timeElapsed, float time, float deltaTime)
    {
        float dampingCoefficient = time == 0.0f ? GetRemainder(timeElapsed) : GetRemainder(timeElapsed) / time;
        float acceleration = dampingCoefficient * dampingCoefficient * value;
        float deltaDamping = 1.0f + dampingCoefficient * deltaTime;
        float newVelocity = (velocity - acceleration * deltaTime) / (deltaDamping * deltaDamping);
        return new Vector3(value + deltaTime * newVelocity, 0, newVelocity);
    }

    Vector3 CalculateCriticallyDampedSpring(Vector3 value, Vector3 velocity, float timeElapsed, float time, float deltaTime)
    {
        float dampingCoefficient = time == 0.0f ? GetRemainder(timeElapsed) : GetRemainder(timeElapsed) / time;
        Vector3 acceleration = dampingCoefficient * dampingCoefficient * value;
        float deltaDamping = 1.0f + dampingCoefficient * deltaTime;
        Vector3 newVelocity = (velocity - acceleration * deltaTime) / (deltaDamping * deltaDamping);
        return newVelocity;
    }

    private float GetRemainder(float timeElapsed)
    {
        int index = 0;
        for (int i = 0; i < lookUpTableW.Length; i++ )
        {
            if (i * stepValue > timeElapsed) return (float)lookUpTableW[index];
            index = i;
        }

        return (float)lookUpTableW[lookUpTableW.Length - 1];
    }

}
