using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace BugReporter
{ 
    public class GithubBugReportBackend : BugReporterBackend
    {
        private static readonly string backendName = "github";
        static readonly string baseURL = "https://api.github.com";

        protected bool _isInit = false;
        protected bool _isLoggedIn = false;
        protected string _token = "";
        protected int _userID;
        protected BugReporterPlugin.UserEntry _userInfo;


        // /!\ Don't forget to add a static constructor to register the backend !!
        static GithubBugReportBackend()
        {
            BugReporterPlugin.RegisterBackend(backendName, new GithubBugReportBackend());
        }

        public override void Init()
        {
            _isLoggedIn = false;
            GetAuthToken();
        }

        public override string GetName()
        {
            return backendName;
        }

        public override bool CanRequest()
        {
            return _isLoggedIn;
        }

        public override void SetProjectPath(string projectPath)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            settings.projectPath = projectPath;

            SetupProjectInfo();
        }

        public override void RequestIssues(Action<List<BugReporterPlugin.IssueEntry>> requestFinishedCallback, BugReporterPlugin.IssueFilter filter)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);

            string requestURL = baseURL + "/repos/" + settings.projectPath + "/issues?";

            if(filter.user != null)
            {
                requestURL += "assignee=" + filter.user.name + "&";
            }

            if (filter.labels != null && filter.labels.Length > 0)
            {
                requestURL += "labels="+filter.labelCommaString;
            }

            var request = UnityWebRequest.Get(requestURL);
            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("Authorization", "token " + _token);

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
                    GithubIssueData[] issues = JsonUtility.FromJson<Wrapper<GithubIssueData>>(newJson).array;

                    for (int i = 0; i < issues.Length; ++i)
                    {
                        BugReporterPlugin.IssueEntry newEntry = new BugReporterPlugin.IssueEntry();
                        newEntry.title = issues[i].title;
                        newEntry.description = issues[i].body;

                        newEntry.assignees = new BugReporterPlugin.UserEntry[0];
                        if (issues[i].assignees != null)
                        {
                            for (int j = 0; j < issues[i].assignees.Length; ++j)
                            {
                                //TODO : this is terrible. Use some dictionnary maybe or better typing to avoid so much silly conversion
                                var userEntry = BugReporterPlugin.GetUserInfoByID(issues[i].assignees[j].id.ToString());
                                if(userEntry != null)
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
            data.AppendFormat("{{\"title\": \"{0}\", \"body\": \"{1}\"", issue.title, issue.description);

            if (issue.assignees.Length > 0)
            {
                data.Append(",\"assignees\":[");

                for(int i = 0; i < issue.assignees.Length; ++i)
                {
                    data.AppendFormat("\"{0}\"", issue.assignees[i].name);
                    if(i != issue.assignees.Length-1)
                    {
                        data.Append(",");
                    }
                }
                data.Append("]");
            }

            if (issue.labels.Length > 0)
            {
                data.Append(",\"labels\":[");

                for (int i = 0; i < issue.labels.Length; ++i)
                {
                    data.AppendFormat("\"{0}\"", issue.labels[i]);
                    if (i != issue.labels.Length - 1)
                    {
                        data.Append(",");
                    }
                }
                data.Append("]");
            }

            data.Append("}");

            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);

            var request = new UnityWebRequest(baseURL + "/repos/" + settings.projectPath + "/issues", "POST");

            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("Authorization", "token " + _token);

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

            if (settings == null || settings.token == "")
            {
                var loginWin = EditorWindow.GetWindow<UsernameLoginWindow>();
                loginWin.onWindowClosed = window =>
                {
                    if (window.validated)
                        TryLogin(window.password);
                };
                loginWin.ShowAuxWindow();
            }
            else
            {
                TryLogin(settings.token);
            }
        }

        protected void TryLogin(string password)
        {
            var request = UnityWebRequest.Get(baseURL+"/user");
            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("Authorization", "token " + password);

            var async = request.SendWebRequest();

            async.completed += op =>
            {
                UnityWebRequestAsyncOperation asyncop = op as UnityWebRequestAsyncOperation;
                if (asyncop.webRequest.isHttpError)
                {
                    Debug.Log(asyncop.webRequest.error);
                    GetAuthToken();
                }
                else
                {
                    string response = asyncop.webRequest.GetResponseHeader("X-OAuth-Scopes");
                    if (response.Contains("repo"))
                    {
                       GithubUserData user = JsonUtility.FromJson<GithubUserData>(asyncop.webRequest.downloadHandler.text);

                        _userID = user.id;

                        var setting = BugReporterPlugin.settings.GetBackendSettings(backendName);
                        setting.token = password;
                        _token = password;
                        _isLoggedIn = true;
                        BugReporterPlugin.SaveSettings();

                        Debug.LogFormat("[Github Backend] : Logged in as user " + user.login);

                        if (setting.projectPath != "")
                            SetupProjectInfo();

                        if (OnPostInit != null)
                            OnPostInit.Invoke();
                    }
                    else 
                    {
                        Debug.LogError("[Github Backend] : This Personal Access token don't have the repo access");
                        GetAuthToken();
                    }
                }
            };
        }

        private void SetupProjectInfo()
        {
            RequestUsers();
            RequestLabels();
        }

        void RequestLabels()
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            var request = UnityWebRequest.Get(baseURL + "/repos/" + settings.projectPath + "/labels");
            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("Authorization", "token " + _token);

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
                    GithubLabel[] labelsData = JsonUtility.FromJson<Wrapper<GithubLabel>>(newJson).array;
                    Debug.LogFormat("[Github Backend] Found {0} labels in that project", labelsData.Length);
                    for (int i = 0; i < labelsData.Length; ++i)
                    {
                        _labels.Add(labelsData[i].name);
                    }
                }
            };
        }

        void RequestUsers()
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            var request = UnityWebRequest.Get(baseURL + "/repos/" + settings.projectPath + "/assignees");
            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("Authorization", "token " + _token);

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
                    GithubUserData[] usersData = JsonUtility.FromJson<Wrapper<GithubUserData>>(newJson).array;
                    Debug.LogFormat("[Github Backend] Found {0} users in that project", usersData.Length);
                    for (int i = 0; i < usersData.Length; ++i)
                    {
                        BugReporterPlugin.UserEntry entry = new BugReporterPlugin.UserEntry();
                        entry.id = usersData[i].id.ToString();
                        entry.name = usersData[i].login;

                        if (usersData[i].id == _userID)
                            _userInfo = entry;

                        _users.Add(entry);
                    }
                }
            };
        }

        [System.Serializable]
        class GithubIssueData
        {
            public string title = "";
            public string body = "";

            public GithubUserData user = null;
            public GithubUserData[] assignees = new GithubUserData[0];
        }

        [System.Serializable]
        public class GithubUserData
        {
            public string login = "";
            public int id = -1;
        }

        [System.Serializable]
        public class GithubLabel
        {
            public string name = "";
        }
    }

    public class UsernameLoginWindow : EditorWindow
    {
        //public string username { get; protected set; }
        public string password { get; protected set; }
        public bool validated { get; protected set; }

        public System.Action<UsernameLoginWindow> onWindowClosed;

        private void OnGUI()
        {
            //username = EditorGUILayout.TextField("Login", username);
            password = EditorGUILayout.PasswordField("OATH token", password);
            EditorGUILayout.HelpBox("Generate a OAuth personal access token on Github with access to : repo, users and past it here", MessageType.Info);

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