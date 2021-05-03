using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MotionJointData 
{
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 velocity;
    public Vector3 angularSpeed;
    public Vector3 direction;

    // Debug 
    public string name;
    public Vector3 position;
    public Quaternion rotation;
    public int index;
}
