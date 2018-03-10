using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BugReporter
{
    public class BugReporterPlugin
    {
        static readonly string configFilePath = "/../Library/BugTrackerSettings.json";

        public enum BackendType
        {
            None,
            Github
        }

        public enum IssueRequestState
        {
            Empty,
            Requesting,
            Completed
        }

        public static BugReporterPluginSettings settings { get { return _settings; } }
        public static BugReporterBackend backend { get { return _backend; } }
        public static IssueRequestState issueRequestState {  get {  return _issueRequestState; } }
        public static List<IssueEntry> issues { get { return _issues; } }

        private static BugReporterBackend _backend;
        private static bool testValue = false;
        private static BugReporterPluginSettings _settings;

        //TODO : find somewhere else to store that
        private static Vector3 _bugReportCameraPos;
        private static float _bugReportCameraDist;
        private static Quaternion _bugReportCameraRotation;
        private static string _bugReportSceneGUID;

        private static IssueRequestState _issueRequestState;

        private static List<IssueEntry> _issues = new List<IssueEntry>();

        static BugReporterPlugin()
        {
            SceneView.onSceneGUIDelegate += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        static void Update(SceneView view)
        {
            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.F11)
            {
                Debug.Log(view.pivot);
                Debug.Log(view.size);
                Debug.Log(view.cameraDistance);
            }

            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.F12)
            {

                _bugReportCameraPos = view.pivot;
                _bugReportCameraDist = view.cameraDistance;
                _bugReportCameraRotation = view.rotation;
                _bugReportSceneGUID = AssetDatabase.AssetPathToGUID(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

                OpenLogIssueWindow();
            }
        }

        static void PlayModeChanged(PlayModeStateChange stateChanged)
        {
            if (stateChanged == PlayModeStateChange.EnteredPlayMode)
            {
                Application.onBeforeRender += PlayModeBeforeRender;
            }
        }

        static void PlayModeBeforeRender()
        {
            if (Input.GetKey(KeyCode.F12))
            {
                _bugReportCameraPos = Camera.main.transform.position;
                _bugReportCameraRotation = Camera.main.transform.rotation;
                _bugReportCameraDist = 0.001f;
                _bugReportSceneGUID = AssetDatabase.AssetPathToGUID(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

                OpenLogIssueWindow();
            }
        }

        public static void Init()
        {
            _issueRequestState = IssueRequestState.Empty;
            SetupBackend(settings.currentBackendType);
        }

        public static void OpenLogIssueWindow()
        {
            if (_backend == null || !_backend.CanRequest())
            {
                if(_backend == null)
                    Init();

                if (_backend == null)
                {//if still null post init, we didn't set a backend at least once, log an error and exit
                    Debug.LogError("You need to set the backend at least once in the BugTracker window before logging a bug!");
                    return;
                }

                _backend.OnPostInit += OpenLogIssueWindow;
                return;
            }

            var win = EditorWindow.GetWindow<LogIssueWindow>();
            win.onWindowClosed = LogIssueWindowClosed;
        }

        public static void LogIssueWindowClosed(LogIssueWindow win)
        {
            win.entry.cameraPosition = _bugReportCameraPos;
            win.entry.cameraRotation = _bugReportCameraRotation;
            win.entry.cameraDistance = _bugReportCameraDist;
            win.entry.sceneGUID = _bugReportSceneGUID;

            win.entry.BuildUnityBTURL();

            win.entry.description += "\n\n" + win.entry.unityBTURL;

            _backend.LogIssue(win.entry);
        }

        public static void SetupBackend(BackendType backendType)
        {
            switch (backendType)
            {
                case BackendType.Github:
                    _backend = new GithubBugReportBackend();
                    _backend.Init();
                    break;
                case BackendType.None:
                        break;
            }

            settings.currentBackendType = backendType;
        }

        public static void RequestIssues()
        {
            _issueRequestState = IssueRequestState.Requesting;
            backend.RequestIssues(IssueRequestFinished);
        }

        [DidReloadScripts]
        static void ScriptReloaded()
        {
            LoadOrCreateSettings();
        }

        static void IssueRequestFinished(List<IssueEntry> entries)
        {
            _issues = entries;
            _issueRequestState = IssueRequestState.Completed;
        }

        static void LoadOrCreateSettings()
        {
            _settings = new BugReporterPluginSettings();
            if (!File.Exists(Application.dataPath + configFilePath))
            {
                SaveSettings();
            }
            else
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(Application.dataPath+configFilePath), _settings);
            }
        }

        public static void SaveSettings()
        {
            File.WriteAllText(Application.dataPath + configFilePath, JsonUtility.ToJson(_settings));
        }

        public class IssueEntry
        {
            public string title;
            public string description;
            public string assignee;
            public string labels;

            public Vector3 cameraPosition;
            public float cameraDistance;
            public Quaternion cameraRotation;
            public string sceneGUID;

            public string unityBTURL;

            public void BuildUnityBTURL()
            {
                unityBTURL = string.Format("unitybt://{0}_{1}_{2}_{3}_{4}_{5}_{6}_{7}_{8}", cameraPosition.x, cameraPosition.y,
                    cameraPosition.z, cameraDistance, cameraRotation.x, cameraRotation.y, cameraRotation.z, cameraRotation.w,
                    sceneGUID);
            }

            //This will retireve info from the unitybt url insid ethe description and remove that from the description
            public void RetrieveDataFromUnityURL()
            {
                int position = description.IndexOf("unitybt://");

                if(position == -1)
                {
                    unityBTURL = "";
                    return;
                }

                int end = description.IndexOf(' ', position);
                if (end == -1)//if couldn't find a space after the url, assume it's cause the unitybt url is finishing the description
                    end = description.Length;

                unityBTURL = description.Substring(position, end - position);
                description = description.Replace(unityBTURL, "");

                string pureData = unityBTURL.Replace("unitybt://", "");
                string[] data = pureData.Split('_');

                cameraPosition = new Vector3(float.Parse(data[0]), float.Parse(data[1]), float.Parse(data[2]));
                cameraDistance = float.Parse(data[3]);
                cameraRotation = new Quaternion(float.Parse(data[4]), float.Parse(data[5]), float.Parse(data[6]), float.Parse(data[7]));
                sceneGUID = data[8];
            }
        }
    }

    [System.Serializable]
    public class BugReporterPluginSettings : ISerializationCallbackReceiver
    {
        //so most class nesting, may need to clean that...
        [System.Serializable]
        public class BackendSetting
        {
            public string token = "";
            public string projectPath = "";
        }

        public BugReporterPlugin.BackendType currentBackendType = BugReporterPlugin.BackendType.None;

        protected Dictionary<string, BackendSetting> _loginTokens = new Dictionary<string, BackendSetting>();

        //Will add a new setting with that backendName if does not exist
        public BackendSetting GetBackendSettings(string backendName)
        {
            if (!_loginTokens.ContainsKey(backendName))
            {
                _loginTokens[backendName] = new BackendSetting();
            }

            return _loginTokens[backendName];
        }

        // serialization
        [SerializeField]
        protected List<string> _loginTokenKeys = new List<string>();
        [SerializeField]
        protected List<BackendSetting> _loginTokenValues = new List<BackendSetting>();

        public void OnAfterDeserialize()
        {
            _loginTokens = new Dictionary<string, BackendSetting>();
            for (int i = 0; i < _loginTokenKeys.Count; ++i)
            {
                _loginTokens[_loginTokenKeys[i]] = _loginTokenValues[i];
            }
        }

        public void OnBeforeSerialize()
        {
            _loginTokenKeys = new List<string>();
            _loginTokenValues = new List<BackendSetting>();
            foreach (var pair in _loginTokens)
            {
                _loginTokenKeys.Add(pair.Key);
                _loginTokenValues.Add(pair.Value);
            }
        }
    }

    public abstract class BugReporterBackend
    {
        //This will only be called once after init is done. You should check if "CanRequest()" return true first.
        //If it does, you can directly do what you need, otherwise, register to that callback.
        public System.Action OnPostInit;

        public abstract bool Init();
        public abstract bool CanRequest();
        public abstract string GetName();
        public abstract void RequestIssues(System.Action<List<BugReporterPlugin.IssueEntry>> requestFinishedCallback);
        public abstract void LogIssue(BugReporterPlugin.IssueEntry issue);
    }

    public class LogIssueWindow : EditorWindow
    {
        public BugReporterPlugin.IssueEntry entry;

        public System.Action<LogIssueWindow> onWindowClosed;

        private float timeScaleSaved;

        private void OnEnable()
        {
            entry = new BugReporterPlugin.IssueEntry();
            if (Application.isPlaying)
            {
                timeScaleSaved = Time.timeScale;
                Time.timeScale = 0;
            }
        }

        private void OnGUI()
        {
            entry.title = EditorGUILayout.TextField("Title", entry.title);
            EditorGUILayout.LabelField("Description");
            entry.description = EditorGUILayout.TextArea(entry.description, GUILayout.MinHeight((150)));
            entry.assignee = EditorGUILayout.TextField("Assignee", entry.assignee);
            entry.labels = EditorGUILayout.TextField("Labels", entry.labels);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Send"))
            {
                onWindowClosed(this);
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                Time.timeScale = timeScaleSaved;
            }
        }
    }
}