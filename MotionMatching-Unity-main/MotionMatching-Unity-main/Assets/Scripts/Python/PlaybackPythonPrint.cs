// outsourced script

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEditor.PackageManager;

public class PlaybackPythonPrint : MonoBehaviour
{
    //public string txtFileName;
    public string textFilePathAndName;
    List<string> frames = new List<string>();
    int currentFrame = -1;

    [Range(1, 100)]
    public int frameRate = 30;

    [Range(1, 100)]
    public int nJoints = 5;

    public delegate void NewFrameDelegate(string frame);
    public NewFrameDelegate NewFrame;

    void ReadString()
    {
        string path = textFilePathAndName;

        StreamReader reader = new StreamReader(path);
        frames.Clear();
        currentFrame = 0;
        Debug.Log(reader.EndOfStream);
        while (!reader.EndOfStream)
        {
            string frame = reader.ReadLine();
            frames.Add(frame);
            //Debug.Log(frame);
        }
        Debug.Log(frames.Count);
        reader.Close();
    }

    // Start is called before the first frame update
    void Start()
    {
        ReadString();
        NewFrame += SimpleDebugRB.Instance.HandleHierarchyInfo;
        StartCoroutine(UpdateFrame());
    }

    string FetchNextFrame()
    {
        UnityEngine.Debug.Assert(frames.Count > 0, "animation file is invalid or was not loaded properly.");
        currentFrame %= frames.Count;
        return frames[currentFrame++];
    }

    IEnumerator UpdateFrame()
    {
        while (true)
        {
            UnityEngine.Debug.Assert(frameRate > 0, "frame rate must be greater than 0.");
            for (int i = 0; i < nJoints; i++)
                NewFrame?.Invoke(FetchNextFrame());
            yield return new WaitForSeconds(1.0f / frameRate);
        }
    }

}
