using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace BugReporter
{
    public class GitlabBugReporterBackend : BugReporterBackend
    {
        public static readonly string backendName = "gitlab";

        protected string _apiPath;
        protected bool _isInit = false;
        protected bool _isLoggedIn = false;
        protected string _token = "";
        protected int _userID;
        protected BugReporterPlugin.UserEntry _userInfo;

        // /!\ Don't forget to add a static constructor to register the backend !!
        static GitlabBugReporterBackend()
        {
            BugReporterPlugin.RegisterBackend(backendName, new GitlabBugReporterBackend());
        }

        public override void Init()
        {
            _isLoggedIn = false;
            GetAuthToken();
        }

        public override void SetProjectPath(string projectPath)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            settings.projectPath = System.Uri.EscapeDataString(projectPath);

            SetupProjectInfo();
        }

        public override bool CanRequest()
        {
            return _isLoggedIn;
        }

        public override string GetName()
        {
            return backendName;
        }

        public override void RequestIssues(Action<List<BugReporterPlugin.IssueEntry>> requestFinishedCallback, BugReporterPlugin.IssueFilter filter)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);

            string requestURL = _apiPath + "/projects/" + settings.projectPath + "/issues?state=opened&";

            if (filter.user != null)
            {
                requestURL += "assignee_id=" + filter.user.id + "&";
            }

            if (filter.labels != null && filter.labels.Length > 0)
            {
                requestURL += "labels=" + filter.labelCommaString;
            }

            var request = UnityWebRequest.Get(requestURL);
            request.SetRequestHeader("PRIVATE-TOKEN", _token);

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.LogError("couldn't get issues for repo " + settings.projectPath);
                    Debug.LogError(asyncop.webRequest.error);
                }
                else
                {
                    _issues.Clear();

                    string newJson = "{ \"array\": " + asyncop.webRequest.downloadHandler.text + "}";
                    GitlabIssueData[] issues = JsonUtility.FromJson<Wrapper<GitlabIssueData>>(newJson).array;

                    for (int i = 0; i < issues.Length; ++i)
                    {
                        BugReporterPlugin.IssueEntry newEntry = new BugReporterPlugin.IssueEntry();
                        newEntry.title = issues[i].title;
                        newEntry.description = issues[i].description;

                        newEntry.assignees = new BugReporterPlugin.UserEntry[0];
                        if (issues[i].assignees != null)
                        {
                            for (int j = 0; j < issues[i].assignees.Length; ++j)
                            {
                                //TODO : this is terrible. Use some dictionnary maybe or better typing to avoid so much silly conversion
                                var userEntry = BugReporterPlugin.GetUserInfoByID(issues[i].assignees[j].id.ToString());
                                if (userEntry != null)
                                    ArrayUtility.Add(ref newEntry.assignees, userEntry);
                            }
                        }

                        newEntry.RetrieveDataFromUnityURL();
                        newEntry.BuildCommaStrings();

                        _issues.Add(newEntry);
                    }

                    requestFinishedCallback(_issues);
                }
            };
        }

        public override void LogIssue(BugReporterPlugin.IssueEntry issue)
        {
            issue.description = issue.description.Replace("\n", "\\n");

            StringBuilder data = new StringBuilder();
            data.AppendFormat("{{\"title\": \"{0}\", \"description\": \"{1}\"", issue.title, issue.description);

            if (issue.assignees.Length > 0)
            {
                data.Append(",\"assignee_ids\":[");

                for (int i = 0; i < issue.assignees.Length; ++i)
                {
                    data.AppendFormat("\"{0}\"", issue.assignees[i].id);
                    if (i != issue.assignees.Length - 1)
                    {
                        data.Append(",");
                    }
                }
                data.Append("]");
            }

            if (issue.labels.Length > 0)
            {
                data.Append(",\"labels\":\"");
                data.Append(issue.labelsString);
                data.Append("\"");
            }

            data.Append("}");

            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);

            var request = new UnityWebRequest(_apiPath + "/projects/" + settings.projectPath + "/issues", "POST");

            request.SetRequestHeader("PRIVATE-TOKEN", _token);

            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(data.ToString()));
            request.uploadHandler.contentType = "application/json";

            request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError || asyncop.webRequest.responseCode != 201)
                {
                    Debug.LogError("couldn't Post issue to repo " + settings.projectPath);
                    Debug.LogError("Error code : " + async.webRequest.responseCode + "\n Error:\n" + asyncop.webRequest.downloadHandler.text);
                }
                else
                {
                    Debug.Log("Issue posted successfully");
                }
            };
        }

        protected void GetAuthToken()
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);

            if (settings == null || settings.token == "" || settings.apiPath == "")
            {
                var loginWin = EditorWindow.GetWindow<GitlabSettingWindow>();
                loginWin.onWindowClosed = window =>
                {
                    if (window.validated)
                        TryLogin(window.apiPath, window.password);
                };

                loginWin.Open((settings != null && settings.apiPath != "") ? settings.apiPath : "https://gitlab.example.com/api/v4/");
            }
            else
            {
                TryLogin(settings.apiPath, settings.token);
            }
        }

        protected void TryLogin(string apiPath, string password)
        {
            apiPath = apiPath.Trim();
            if (apiPath[apiPath.Length - 1] == '/')
                apiPath = apiPath.Remove(apiPath.Length - 1);

            var request = UnityWebRequest.Get(apiPath + "/user");
            request.SetRequestHeader("PRIVATE-TOKEN", password);

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.Log(asyncop.webRequest.error + " :: " + asyncop.webRequest.downloadHandler.text);
                    GetAuthToken();
                }
                else
                {
                    GitlabUserData user = JsonUtility.FromJson<GitlabUserData>(asyncop.webRequest.downloadHandler.text);

                    _userID = user.id;

                    var setting = BugReporterPlugin.settings.GetBackendSettings(backendName);
                    setting.token = password;
                    setting.apiPath = apiPath;

                    _apiPath = apiPath;
                    _token = password;
                    _isLoggedIn = true;
                    BugReporterPlugin.SaveSettings();

                    Debug.LogFormat("[Gitlab Backend] : Logged in as user " + user.name);

                    // Gitlab have an image uploader setup by default
                    BugReporterPlugin.SetupImageUploader(GitlabImageUploader.imgUploaderName);

                    if (setting.projectPath != "")
                        SetupProjectInfo();

                    if (OnPostInit != null)
                        OnPostInit.Invoke();
                }
            };
        }

        private void SetupProjectInfo()
        {
            RequestUsers();
            RequestLabels();
        }

        void RequestUsers()
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            var request = UnityWebRequest.Get(_apiPath + "/projects/" + settings.projectPath + "/members");
            request.SetRequestHeader("PRIVATE-TOKEN", _token);

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.Log(asyncop.webRequest.error);
                }
                else
                {
                    _users.Clear();

                    string newJson = "{ \"array\": " + asyncop.webRequest.downloadHandler.text + "}";
                    GitlabUserData[] usersData = JsonUtility.FromJson<Wrapper<GitlabUserData>>(newJson).array;
                    Debug.LogFormat("[Gitlab Backend] Found {0} users in that project", usersData.Length);
                    for (int i = 0; i < usersData.Length; ++i)
                    {
                        BugReporterPlugin.UserEntry entry = new BugReporterPlugin.UserEntry();
                        entry.id = usersData[i].id.ToString();
                        entry.name = usersData[i].name;

                        if (usersData[i].id == _userID)
                            _userInfo = entry;

                        _users.Add(entry);
                    }
                }
            };
        }

        void RequestLabels()
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            var request = UnityWebRequest.Get(_apiPath + "/projects/" + settings.projectPath + "/labels");
            request.SetRequestHeader("PRIVATE-TOKEN", _token);

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.Log(asyncop.webRequest.error);
                }
                else
                {
                    _labels.Clear();

                    string newJson = "{ \"array\": " + asyncop.webRequest.downloadHandler.text + "}";
                    GitlabLabelData[] labelsDataData = JsonUtility.FromJson<Wrapper<GitlabLabelData>>(newJson).array;
                    Debug.LogFormat("[Gitlab Backend] Found {0} labels in that project", labelsDataData.Length);
                    for (int i = 0; i < labelsDataData.Length; ++i)
                    {
                        _labels.Add(labelsDataData[i].name);
                    }
                }
            };
        }

        [System.Serializable]
        public class GitlabUserData
        {
            public int id;
            public string name;
        }

        [System.Serializable]
        public class GitlabLabelData
        {
            public int id;
            public string name;
        }

        [System.Serializable]
        public class GitlabIssueData
        {
            public string title;
            public string description;
            public GitlabUserData[] assignees = new GitlabUserData[0];
        }

        public class GitlabSettingWindow : EditorWindow
        {
            public string apiPath { get; protected set; }
            public string password { get; protected set; }
            public bool validated { get; protected set; }

            public System.Action<GitlabSettingWindow> onWindowClosed;

            public void Open(string defaultAPIPath)
            {
                apiPath = defaultAPIPath;
                ShowAuxWindow();
            }

            private void OnGUI()
            {
                apiPath = EditorGUILayout.TextField("Path to API", apiPath);
                password = EditorGUILayout.PasswordField("OATH token", password);
                EditorGUILayout.HelpBox("Generate a personal access token on Gitlab with access to : api", MessageType.Info);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Log In"))
                {
                    validated = true;
                    Close();
                    onWindowClosed(this);
                }

                if (GUILayout.Button("Cancel"))
                {
                    validated = false;
                    Close();
                    onWindowClosed(this);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    public class GitlabImageUploader : ImageUploader
    {
        public static readonly string imgUploaderName = "gitlab-uploader";

        static GitlabImageUploader()
        {
            BugReporterPlugin.RegisterImageUploader(imgUploaderName, new GitlabImageUploader());
        }

        public override void UploadFile(byte[] data, Action<bool, string> onUploadFinished)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(GitlabBugReporterBackend.backendName);

            if (settings.token == "" || settings.projectPath == "" || settings.apiPath == "")
            {
               Debug.LogError("[Gitlab Image Uploader] : Image upload to Gitlab can only be used with Gitlab backend, please setup the gitlab backend presonal token first.");
            }
            else
            {
                InternalUpload(data, onUploadFinished);
            }
        }

        private void InternalUpload(byte[] data, Action<bool, string> onUploadFinished)
        {
            string screenpath = Application.dataPath + "/../Temp/bugscreenshot.png";
            System.IO.File.WriteAllBytes(screenpath, data);

            var settings = BugReporterPlugin.settings.GetBackendSettings(GitlabBugReporterBackend.backendName);

            WWWForm formData = new WWWForm();
            formData.AddBinaryData("file", data ,screenpath);

            var request =
                UnityWebRequest.Post(settings.apiPath + "/projects/" + settings.projectPath + "/uploads", formData);

            request.SetRequestHeader("PRIVATE-TOKEN", settings.token);

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.LogErrorFormat("[Gitlab Image Uploader] Error {0} : {1} ", asyncop.webRequest.responseCode, asyncop.webRequest.downloadHandler.text);
                    onUploadFinished(false, "");
                }
                else
                {
                    GitlabUploadResponse response = JsonUtility.FromJson<GitlabUploadResponse>(asyncop.webRequest.downloadHandler.text);

                    onUploadFinished(true, response.url);
                }
            };
        }

        [System.Serializable]
        private class GitlabUploadResponse
        {
            public string url;
        }
    }
}