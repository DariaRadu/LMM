// outsourced script

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
public class PythonLauncher : MonoBehaviour
{
    public enum ProcessStatus {Uninitialized, Running, Terminated};

    [SerializeField]
    private string _scriptPath;
    [SerializeField]
    private string _pythonInterpreterPath;
    private Process python;
    [SerializeField]
    private string _arguments = "";
    [SerializeField]
    private ProcessStatus _processStatus = ProcessStatus.Uninitialized;

    private char[] _dataKeywords = { 'H', 'O', 'J', 'G', 'X', 'T' };
    public static List<string> data = new List<string>();
    [SerializeField]
    private int _dataCount;

    private bool _processing = false;

    private bool paused = false;
    void Start()
    {
        python = new Process();
        python.StartInfo.FileName = _pythonInterpreterPath;
        python.StartInfo.Arguments = _scriptPath + " " + _arguments;
        python.StartInfo.CreateNoWindow = true;
        python.StartInfo.UseShellExecute = false;
        python.StartInfo.RedirectStandardInput = true;
        python.StartInfo.RedirectStandardOutput = true;
        python.StartInfo.RedirectStandardError = true;
        python.StartInfo.WorkingDirectory = Path.GetDirectoryName(_scriptPath);
        python.OutputDataReceived += OutputDataReceived;
        python.ErrorDataReceived += OutputDataReceived;

        if(python.Start())
        {
            UnityEngine.Debug.Log("Launched python script " + _scriptPath);
            _processStatus = ProcessStatus.Running;
        } else
        {
            throw new UnityException("Could not start the python process!");
        }

        python.BeginErrorReadLine();
        python.BeginOutputReadLine();
    }

    private void PrintDebugInformation(string msg)
    {
        UnityEngine.Debug.Log("Python process: " + msg);
    }

    

    private void OutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!paused && _dataKeywords.Contains(e.Data.ElementAt(0))) data.Add(e.Data);
        else PrintDebugInformation(e.Data);
    }

    void OnApplicationQuit()
    {   
        if(!python.HasExited)
        {
            python.Kill();
        }
    }
    Task readTillEndtask;
    void Update()
    {
        if(python.HasExited)
        {
            _processStatus = ProcessStatus.Terminated;
            
            if(readTillEndtask == null) 
            {
                UnityEngine.Debug.Log("Starting exit code reading task");
                readTillEndtask = new Task(async () => {
                    UnityEngine.Debug.Log("Awaiting exit code");
                    string output = await python.StandardOutput.ReadToEndAsync();
                    string error = await python.StandardError.ReadToEndAsync();
                    PrintDebugInformation(output);
                    PrintDebugInformation(error);
                    UnityEngine.Debug.Log("END");
                });
                readTillEndtask.Start();
            }
            
        }
        _dataCount = data.Count;
    }
    
    void OnApplicationPause(bool istrue)
    {
        paused = istrue;
    }
}

