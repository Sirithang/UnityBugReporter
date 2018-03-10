using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        public override bool Init()
        {
            _isLoggedIn = false;
            GetAuthToken();
            return true;
        }

        public override string GetName()
        {
            return backendName;
        }

        public override bool CanRequest()
        {
            return _isLoggedIn;
        }

        public override void RequestIssues(Action<List<BugReporterPlugin.IssueEntry>> requestFinishedCallback)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            var request = UnityWebRequest.Get(baseURL + "/repos/" + settings.projectPath + "/issues");
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
                    string newJson = "{ \"array\": " + asyncop.webRequest.downloadHandler.text + "}";
                    GithubIssueData[] issues = JsonUtility.FromJson<Wrapper<GithubIssueData>>(newJson).array;
                    List<BugReporterPlugin.IssueEntry> issueEntries = new List<BugReporterPlugin.IssueEntry>();
                    Debug.LogFormat("Found {0} issues", issues.Length);
                    for (int i = 0; i < issues.Length; ++i)
                    {
                        BugReporterPlugin.IssueEntry newEntry = new BugReporterPlugin.IssueEntry();
                        newEntry.title = issues[i].title;
                        newEntry.description = issues[i].body;

                        if (issues[i].assignees != null)
                        {
                            for (int j = 0; j < issues[i].assignees.Length; ++j)
                            {
                                newEntry.assignee += issues[i].assignees[j].login + ";";
                            }
                        }

                        newEntry.RetrieveDataFromUnityURL();

                        issueEntries.Add(newEntry);
                    }

                    requestFinishedCallback(issueEntries);
                }
            };
        }

        public override void LogIssue(BugReporterPlugin.IssueEntry issue)
        {
            issue.description = issue.description.Replace("\n", "\\n");

            string json = string.Format(
                "{{\"title\": \"{0}\", \"body\": \"{1}\"}}",
                issue.title, issue.description);

            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);

            var request = new UnityWebRequest(baseURL + "/repos/" + settings.projectPath + "/issues", "POST");

            request.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            request.SetRequestHeader("Authorization", "token " + _token);

            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
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
            var request = UnityWebRequest.Get(baseURL);
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
                    if (true)
                    {
                        Debug.Log("Valid login info");

                        var setting = BugReporterPlugin.settings.GetBackendSettings(backendName);
                        setting.token = password;
                        _token = password;
                        _isLoggedIn = true;
                        BugReporterPlugin.SaveSettings();

                        if(OnPostInit != null)
                            OnPostInit.Invoke();
                    }
                    else 
                    {
                        Debug.LogError("invialid login info " + response);
                        GetAuthToken();
                    }
                }
            };
        }

        [System.Serializable]
        class GithubIssueData
        {
            public string title;
            public string body;

            public UserIssueData user;
            public UserIssueData[] assignees;

            [System.Serializable]
            public class UserIssueData
            {
                public string login;
            }
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] array;
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