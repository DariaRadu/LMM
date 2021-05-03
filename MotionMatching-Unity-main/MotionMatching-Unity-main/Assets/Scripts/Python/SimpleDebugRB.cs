// outsourced script

using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// this behavior is used to help debugging the incomming packages.
/// it creates a simple graphical representations for all rigid bodies received 
/// through the network
/// </summary>
public class SimpleDebugRB : MonoBehaviour {

    public static SimpleDebugRB Instance;

    Dictionary<string, Dictionary<string, Transform>> skeletons = new Dictionary<string, Dictionary<string, Transform>>();

    Dictionary<string, string> _childParentMapping = new Dictionary<string, string>();
    Dictionary<Transform,Transform> _debugDrawHierarchy = new Dictionary<Transform, Transform>();
    public GameObject skeleton;

    public Material matPrediction, matGroundTruth;

    private List<float> _predictionTimes = new List<float>();
    private int _maxPredictionTimeCount = 1000;
    private float _minPredictionTime = 1.0f;
    private float _maxPredictionTime = 0.0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;
    }

    void DrawLinks()
    {
        foreach(var skeleton_name in skeletons.Keys)
        {
            var skeleton = skeletons[skeleton_name];
            foreach(var joint in skeleton.Values)
            {
                if(joint.name.Trim().Equals("Hips"))
                {
                    continue;
                } 
                if(joint.transform.parent != null && !joint.transform.parent.name.Equals("Hips"))
                {
                    Debug.DrawLine(joint.position, joint.transform.parent.position, matGroundTruth.color);
                }
                else if(!_debugDrawHierarchy.ContainsKey(joint)) //Global space joint has no transform parent
                {
                    string parentName = _childParentMapping[joint.name];
                    if(parentName.Trim().Equals("Hips"))
                    {
                        continue;
                    }
                    GameObject parent = null;
                    while(parent == null)
                    {
                        foreach(GameObject go in GameObject.FindObjectsOfType<GameObject>())
                        {
                            if(go.name.Equals(parentName))
                            {
                                if(go.transform.parent != null && !go.transform.parent.name.Equals("Hips")) continue;
                                if(skeleton.ContainsValue(go.transform))
                                {
                                    parent = go;
                                    break;
                                }
                            }
                        }
                        parentName = _childParentMapping[parentName];
                        if(parentName.Trim().Equals("Hips"))
                        {
                            continue;
                        }
                    }
                    if(parent != null)
                    {
                        _debugDrawHierarchy.Add(joint, parent.transform);
                    }
                } else 
                {
                    Debug.DrawLine(joint.position, _debugDrawHierarchy[joint].position, matPrediction.color);
                }
                
            } 
        }
    }

    void Update()
    {
        while (PythonLauncher.data.Count > 0)
        {
            try
            {
                HandleHierarchyInfo(PythonLauncher.data[0]);
                PythonLauncher.data.RemoveAt(0);

            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Something went wrong with: " + PythonLauncher.data[0] + "\n" + e.Message + "\n" + e.StackTrace);
                PythonLauncher.data.RemoveAt(0);
            }

        }

       // DrawLinks();
    }


    float parsefloat(string s) 
    {
        return float.Parse(s, CultureInfo.InvariantCulture.NumberFormat);
    }

    public void HandleHierarchyInfo(string info)
    {
        string[] infos = info.Split(' ');
        switch(infos[0])
        {
            case "H":
            ProcessHierarchyInfo(infos);
            break;
            case "O":
            ProcessJointOffsets(infos);
            break;
            case "J":
            ProcessSkeletonJointData(infos);
            break;
            case "G":
            ProcessGlobalJointData(infos);
            break;
            case "X":
            ProcessAngleAxisSkeletonJointData(infos);
            break;
            case "T":
            UpdatePredictionTime(infos);
            break;
            default:
            throw new System.Exception("Python input not recognized!");
        }
        
    }

    void UpdatePredictionTime(string[] info)
    {
        float predictionTime = parsefloat(info[1]);
        if(_predictionTimes.Count > 50)
        {
            _maxPredictionTime = Mathf.Max(_maxPredictionTime, predictionTime);
            _minPredictionTime = Mathf.Min(_minPredictionTime, predictionTime);
        }

        _predictionTimes.Add(predictionTime);
        if(_predictionTimes.Count  > _maxPredictionTimeCount) _predictionTimes.RemoveAt(0);
    }

    void ProcessHierarchyInfo(string[] info)
    {
        string hierarchyName = info[1];
        skeletons.Add(hierarchyName, new Dictionary<string, Transform>());
        
        for(uint pairIndex = 2; pairIndex < info.Length; pairIndex++)
        {
            string[] childParent = info[pairIndex].Split('-');
            if(childParent.Length < 2) break;
            string child = childParent[0];
            string parent = childParent[1];
            if(!_childParentMapping.ContainsKey(child))
            {
                _childParentMapping.Add(child, parent);
            } 
            if(!parent.Equals("") && !skeletons[hierarchyName].ContainsKey(parent))
            {
                GameObject parentJoint = CreateJoint(parent, info[1]);
                skeletons[hierarchyName].Add(parent, parentJoint.transform);
            }
            if(!skeletons[hierarchyName].ContainsKey(child))
            {
                GameObject childJoint = CreateJoint(child, info[1]);
                if(!parent.Equals("")) childJoint.transform.parent = skeletons[hierarchyName][parent];
                skeletons[hierarchyName].Add(child, childJoint.transform);
            }
        }
    }

    void ProcessJointOffsets(string[] info)
    {
        string hierarchyName = info[1];
        string jointName = info[2];
        skeletons[hierarchyName][jointName].localPosition = new Vector3(parsefloat(info[3]), parsefloat(info[4]), parsefloat(info[5]));
    }

    void ProcessSkeletonJointData(string[] info)
    {
        string skeletonName = info[1];
        string jointName = info[2];
        Vector3 position = new Vector3(parsefloat(info[3]), parsefloat(info[4]), parsefloat(info[5]));
        Vector3 rotation = new Vector3(parsefloat(info[6]), parsefloat(info[7]), parsefloat(info[8]));
        UpdateRigidBody(skeletonName, jointName, position, rotation);
    }

    void ProcessAngleAxisSkeletonJointData(string[] info)
    {
        string skeletonName = info[1];
        string jointName = info[2];
        Vector3 position = new Vector3(parsefloat(info[3]), parsefloat(info[4]), parsefloat(info[5]));
        Vector3 axis = new Vector3(parsefloat(info[6]), parsefloat(info[7]), parsefloat(info[8])).normalized;
        float angle = Mathf.Rad2Deg * parsefloat(info[9]);
        UpdateRigidBody_AngleAxis(skeletonName, jointName, position, axis, angle);
    }

    void ProcessGlobalJointData(string[] info)
    {
        string skeletonName = info[1];
        string jointName = info[2];
        Vector3 position = new Vector3(parsefloat(info[3]), parsefloat(info[4]), parsefloat(info[5]));
        //        Vector3 rotation = new Vector3(parsefloat(info[6]), parsefloat(info[7]), parsefloat(info[8]));
        // Quaternion rotation = new Quaternion(parsefloat(info[6]), parsefloat(info[7]), parsefloat(info[8]), parsefloat(info[9]));
        Vector3 i = new Vector3(parsefloat(info[10]), parsefloat(info[11]), parsefloat(info[12]));
        Vector3 j = new Vector3(parsefloat(info[13]), parsefloat(info[14]), parsefloat(info[15]));
        Vector3 k = Vector3.Cross(i, j);
        Quaternion rotation = Quaternion.LookRotation(k, j);
        if (!skeletons.ContainsKey(skeletonName))
        {
            skeletons.Add(skeletonName, new Dictionary<string, Transform>());
        }
        if(!skeletons[skeletonName].ContainsKey(jointName))
        {
            GameObject go = GameObject.Find(jointName);
            skeletons[skeletonName][jointName] = go != null ? go.transform : CreateJoint(jointName, skeletonName).transform;
        }
        UpdateRigidBody(skeletonName, jointName, position, rotation);
    }

    public void UpdateRigidBody(string skeletonName, string jointName, Vector3 position, Quaternion quat)
    {
        /*if(skeletons[skeletonName][jointName].parent == null)
        {
            skeletons[skeletonName][jointName].localPosition = position;
        }*/
        skeletons[skeletonName][jointName].localPosition = position;
        skeletons[skeletonName][jointName].localRotation = quat;

    }

    public void UpdateRigidBody(string skeletonName, string jointName, Vector3 position, Vector3 eulerAngles)
    {
        /*if(skeletons[skeletonName][jointName].parent == null)
        {
            skeletons[skeletonName][jointName].localPosition = position;
        }*/
        skeletons[skeletonName][jointName].localPosition = position;
        skeletons[skeletonName][jointName].localRotation =
                                                    Quaternion.AngleAxis(eulerAngles.z, Vector3.forward) *
                                                    Quaternion.AngleAxis(eulerAngles.y, Vector3.up) *
                                                    Quaternion.AngleAxis(eulerAngles.x, Vector3.right);
    }

    public void UpdateRigidBody_AngleAxis(string skeletonName, string jointName, Vector3 position, Vector3 axis, float angle)
    {
        if(skeletons[skeletonName][jointName].parent == null)
        {
            skeletons[skeletonName][jointName].localPosition = position;
        } 
        skeletons[skeletonName][jointName].localRotation = Quaternion.AngleAxis(angle, axis);
    }
	
	/// <summary>
	/// creates a graphical representation in the shape of a shoebox.
	/// </summary>
	/// <returns>The shoe box.</returns>
	/// <param name="scale">A scale multiplier.</param>
    GameObject CreateJoint(string name, string skeletonName="", float scale = 1)
    {
        GameObject newGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
//        GameObject newGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (skeletonName.Equals("prediction") ) {
            newGO.GetComponent<Renderer>().material = matPrediction;
        }
        else
        {
            newGO.GetComponent<Renderer>().material = matGroundTruth;
        }
        newGO.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f) * scale;
        newGO.name = name;

        return newGO;
    }

    void OnGUI() {
        if(_predictionTimes.Count == _maxPredictionTimeCount)
        {
            decimal _predictionTime = 0;
            foreach(float t in _predictionTimes)
            {
                _predictionTime += (decimal)t;
            }
            
            _predictionTime /= _predictionTimes.Count;
            int fps = (int)(1.0m/_predictionTime);
            GUILayout.TextField(fps.ToString());
            _predictionTime = decimal.Round(_predictionTime, 5);
            GUILayout.TextField(_predictionTime.ToString());
            
        }
        decimal min = decimal.Round((decimal)_minPredictionTime, 5);
        decimal max = decimal.Round((decimal)_maxPredictionTime, 5);
        GUILayout.TextField("Min prediction time: " + min.ToString());
        GUILayout.TextField("Max prediction time: " + max.ToString());
    }
}
