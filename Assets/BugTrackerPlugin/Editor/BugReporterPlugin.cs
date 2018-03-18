using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace BugReporter
{
    public class BugReporterPlugin
    {
        static readonly string configFilePath = "/../Library/BugTrackerSettings.json";

        public enum IssueRequestState
        {
            Empty,
            Requesting,
            Completed
        }

        public static BugReporterPluginSettings settings { get { return _settings; } }
        public static BugReporterBackend backend { get { return _backend; } }
        public static IssueRequestState issueRequestState { get { return _issueRequestState; } }

        public static List<IssueEntry> issues { get { return _backend.issues; } }
        public static List<UserEntry> users { get { return _backend.users; } }
        public static List<string> labels { get { return _backend.labels; } }

        private static BugReporterBackend _backend;
        private static BugReporterPluginSettings _settings;

        private static ImageUploader _imgUploader = null;

        private static Dictionary<string, BugReporterBackend> _registeredBackends = new Dictionary<string, BugReporterBackend>();
        //easier to query for a list of name that having to convert the keys from the dictionnary everytime
        private static string[] _backendsName = new string[0];

        private static Dictionary<string, ImageUploader> _registeredImageUploader = new Dictionary<string, ImageUploader>();
        //easier to query for a list of name that having to convert the keys from the dictionnary everytime
        private static string[] _imageUploaderName = new string[0];

        //TODO : find somewhere else to store that maybe
        private static Vector3 _bugReportCameraPos;
        private static float _bugReportCameraDist;
        private static Quaternion _bugReportCameraRotation;
        private static string _bugReportSceneGUID;
        private static byte[] _screenShot;

        private static IssueRequestState _issueRequestState;

        static BugReporterPlugin() 
        {
            //find all possible backend : 
            var subclasses =
            from assembly in AppDomain.CurrentDomain.GetAssemblies()
            from type in assembly.GetTypes()
            where type.IsSubclassOf(typeof(BugReporterBackend))
            select type;

            foreach(var t in subclasses)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            }

            //find all possible imageUploader : 
            subclasses =
            from assembly in AppDomain.CurrentDomain.GetAssemblies()
            from type in assembly.GetTypes()
            where type.IsSubclassOf(typeof(ImageUploader))
            select type;

            foreach (var t in subclasses)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            }

            SceneView.onSceneGUIDelegate += Update;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        static public void RegisterBackend(string name, BugReporterBackend backend)
        {
            _registeredBackends[name] = backend;
            ArrayUtility.Add(ref _backendsName, name);
        }

        static public void RegisterImageUploader(string name, ImageUploader uploader)
        {
            _registeredImageUploader[name] = uploader;
            ArrayUtility.Add(ref _imageUploaderName, name);
        }

        static public string[] GetImageUploaderName()
        {
            return _imageUploaderName;
        }

        static public string[] GetBackendNameList()
        {
            return _backendsName;
        }

        static void Update(SceneView view)
        {
            if (Event.current.type == EventType.KeyUp && Event.current.keyCode == KeyCode.F12)
            {

                _bugReportCameraPos = view.pivot;
                _bugReportCameraDist = view.cameraDistance;
                _bugReportCameraRotation = view.rotation;
                _bugReportSceneGUID = AssetDatabase.AssetPathToGUID(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

                _screenShot = EncodeToPNG(view.camera.activeTexture);

                OpenLogIssueWindow();
            }
        }

        static byte[] EncodeToPNG(RenderTexture rt)
        {
            Texture2D tex;

            if(rt == null)
                tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGBA32, false);
            else
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            tex.Apply();

            return ImageConversion.EncodeToPNG(tex);
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

                _screenShot = EncodeToPNG(Camera.main.activeTexture);

                OpenLogIssueWindow();
            }
        }

        public static void Init()
        {
            _issueRequestState = IssueRequestState.Empty;
            SetupBackend(settings.currentBackendType);
            SetupImageUploader(settings.currentImageUploader);
        }

        public static void SetProjectPath(string projectPath)
        {
            _backend.SetProjectPath(projectPath);
            SaveSettings();
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

            _backend.OnPostInit -= OpenLogIssueWindow;
            var win = EditorWindow.GetWindow<BugReportWindow>();
            win.onWindowClosed = LogIssueWindowClosed;
        }

        public static void LogIssueWindowClosed(BugReportWindow win)
        {
            win.entry.cameraPosition = _bugReportCameraPos;
            win.entry.cameraRotation = _bugReportCameraRotation;
            win.entry.cameraDistance = _bugReportCameraDist;
            win.entry.sceneGUID = _bugReportSceneGUID;

            if (win.logPosition)
                win.entry.BuildUnityBTURL();
            else
                win.entry.unityBTURL = "";

            win.entry.BuildCommaStrings();

            if (_imgUploader!= null && win.uploadScreenshot)
            {
                _imgUploader.UploadFile(_screenShot, (valid, url) =>
                {
                    bool upload = true;
                    if (valid)
                    {
                        win.entry.description += string.Format("\n\n![Screenshot]({0})", url);
                    }
                    else
                    {
                        upload = EditorUtility.DisplayDialog("Fail screenshot upload",
                            "Uplaoding the screenshot failed, do you still want to log the issue without the screenshot?",
                            "Yes", "No");
                    }

                    if (upload)
                    {
                        if (win.logPosition)
                            win.entry.description += "\n\n" + win.entry.unityBTURL;

                        _backend.LogIssue(win.entry);
                    }
                });

            }
            else
            {
                if (win.logPosition)
                    win.entry.description += "\n\n" + win.entry.unityBTURL;

                _backend.LogIssue(win.entry);
            }
        }

        public static void SetupBackend(string backendType)
        {
            if (backendType == "")
                return;

            if(!_registeredBackends.ContainsKey(backendType))
            {
                Debug.LogError("Couldn't find a backend with the name " + backendType);
                settings.currentBackendType = "";
                SaveSettings();
                return;
            }

            _backend = _registeredBackends[backendType];
            _backend.Init();

            settings.currentBackendType = backendType;
        }

        public static void SetupImageUploader(string imageUploaderType)
        {
            if (imageUploaderType == "")
                return;

            if (!_registeredImageUploader.ContainsKey(imageUploaderType))
            {
                Debug.LogError("Couldn't find a image uploaded with the name " + imageUploaderType);
                settings.currentImageUploader = "";
                SaveSettings();
                return;
            }

            _imgUploader = _registeredImageUploader[imageUploaderType];

            settings.currentImageUploader = imageUploaderType;
            SaveSettings();
        }

        public static void RequestIssues(System.Action<List<IssueEntry>> receivedCallback, IssueFilter filter)
        {
            _issueRequestState = IssueRequestState.Requesting;
            backend.RequestIssues(entries =>
            {
                _issueRequestState = IssueRequestState.Completed;
                if(receivedCallback != null)
                {
                    receivedCallback(entries);
                }
            },
            filter
            );
        }

        public static UserEntry GetUserInfoByID(string userID)
        {
            return users.Find(user => { return user.id == userID; });
        }

        public static UserEntry GetUserInfoByName(string username)
        {
            return users.Find(user => { return user.name == username; });
        }

        [DidReloadScripts]
        static void ScriptReloaded()
        {
            LoadOrCreateSettings();
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

        public class IssueFilter
        {
            public UserEntry user = null;
            public string[] labels = new string[0];

            public string labelCommaString = "";

            public void BuildLabelCommaString()
            {
                labelCommaString = "";
                for(int i = 0; i < labels.Length; ++i)
                {
                    labelCommaString += labels[i];
                    if (i != labels.Length - 1)
                        labelCommaString += ",";
                }
            }
        }

        public class IssueEntry
        {
            //useful to store backend-specific id
            public string id;

            public string title;
            public string description;
            public string assigneesString;
            public string labelsString;

            public int severity = -1;

            public string webUrl = "";

            public UserEntry[] assignees = new UserEntry[0];
            public string[] labels = new string[0];

            public Vector3 cameraPosition;
            public float cameraDistance;
            public Quaternion cameraRotation;
            public string sceneGUID;

            public string unityBTURL;

            public void ParseCommaStrings()
            {
                string[] assigneeNames = assigneesString.Split(',');
                assignees = new UserEntry[0];
                for (int i = 0; i < assigneeNames.Length; ++i)
                {
                    var user = GetUserInfoByName(assigneeNames[i]);
                    if (user != null)
                        ArrayUtility.Add(ref assignees, user);
                }

                labels = labelsString.Split(',');
            }

            public void BuildCommaStrings()
            {
                assigneesString = "";
                for (int i = 0; i < assignees.Length; ++i)
                    assigneesString += assignees[i].name + ((i == assignees.Length - 1) ? "" : ",");

                labelsString = string.Join(",", labels);
            }

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

        public class UserEntry
        {
            //backend specific id
            public string id;
            public string name;
        }
    }

    [System.Serializable]
    public class BugReporterPluginSettings : ISerializationCallbackReceiver
    {
        //so many class nesting, may need to clean that...
        [System.Serializable]
        public class BackendSetting
        {
            public string token = "";
            public string projectPath = "";
            public string apiPath = "";
        }

        [System.Serializable]
        public class ImageUploaderSetting
        {
            public string authentification = "";
        }

        public string currentBackendType = "";
        public string currentImageUploader = "";

        protected Dictionary<string, BackendSetting> _loginTokens = new Dictionary<string, BackendSetting>();
        protected Dictionary<string, ImageUploaderSetting> _imgUploaderSettings = new Dictionary<string, ImageUploaderSetting>();

        //Will add a new setting with that backendName if does not exist
        public BackendSetting GetBackendSettings(string backendName)
        {
            if (!_loginTokens.ContainsKey(backendName))
            {
                _loginTokens[backendName] = new BackendSetting();
            }

            return _loginTokens[backendName];
        }

        //Will add a new setting with that imageUploaderName if does not exist
        public ImageUploaderSetting GetImageUploaderSetting(string imageUploaderName)
        {
            if (!_imgUploaderSettings.ContainsKey(imageUploaderName))
            {
                _imgUploaderSettings[imageUploaderName] = new ImageUploaderSetting();
            }

            return _imgUploaderSettings[imageUploaderName];
        }

        // serialization
        [SerializeField]
        protected List<string> _loginTokenKeys = new List<string>();
        [SerializeField]
        protected List<BackendSetting> _loginTokenValues = new List<BackendSetting>();

        [SerializeField]
        protected List<string> _imgUploaderKeys = new List<string>();
        [SerializeField]
        protected List<ImageUploaderSetting> _imgUploaderValues = new List<ImageUploaderSetting>();

        public void OnAfterDeserialize()
        {
            _loginTokens = new Dictionary<string, BackendSetting>();
            for (int i = 0; i < _loginTokenKeys.Count; ++i)
            {
                _loginTokens[_loginTokenKeys[i]] = _loginTokenValues[i];
            }

            _imgUploaderSettings = new Dictionary<string, ImageUploaderSetting>();
            for(int  i = 0; i < _imgUploaderKeys.Count; ++i)
            {
                _imgUploaderSettings[_imgUploaderKeys[i]] = _imgUploaderValues[i];
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

            _imgUploaderKeys = new List<string>();
            _imgUploaderValues = new List<ImageUploaderSetting>();
            foreach(var pair in _imgUploaderSettings)
            {
                _imgUploaderKeys.Add(pair.Key);
                _imgUploaderValues.Add(pair.Value);
            }
        }
    }

    //Subclass that to write an image uploader.
    public abstract class ImageUploader
    {
        //should upload the image and call onUploadFinished when done.
        //1st param must be true if upload succeed false otherwise, 2nd param is link to picture if succeed
        public abstract void UploadFile(byte[] data, System.Action<bool, string> onUploadFinished);
    }

    //used by class that receive JSON that are array without an encapsulating object. JSONUtility can't deserialize those, so we use that class
    [Serializable]
    public class Wrapper<T>
    {
        public T[] array = null;
    }
}