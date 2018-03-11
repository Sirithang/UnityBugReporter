﻿using System;
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
        protected int _userID;
        protected BugReporterPlugin.UserEntry _userInfo;

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

        public override BugReporterPlugin.UserEntry GetCurrentUserInfo()
        {
            return _userInfo;
        }

        public override BugReporterPlugin.UserEntry GetUserInfoByID(string userID)
        {
            return BugReporterPlugin.users.Find(user => { return user.id == userID; });
        }

        public override BugReporterPlugin.UserEntry GetUserInfoByName(string username)
        {
            return BugReporterPlugin.users.Find(user => { return user.name == username; });
        }

        public override void SetProjectPath(string projectPath)
        {
            var settings = BugReporterPlugin.settings.GetBackendSettings(backendName);
            settings.projectPath = projectPath;

            SetupProjectInfo();
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
                    _issues.Clear();

                    string newJson = "{ \"array\": " + asyncop.webRequest.downloadHandler.text + "}";
                    GithubIssueData[] issues = JsonUtility.FromJson<Wrapper<GithubIssueData>>(newJson).array;
                    Debug.LogFormat("Found {0} issues", issues.Length);
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
                                var userEntry = GetUserInfoByID(issues[i].assignees[j].id.ToString());
                                if(userEntry != null)
                                    ArrayUtility.Add(ref newEntry.assignees, userEntry);
                            }
                        }

                        newEntry.RetrieveDataFromUnityURL();
                        newEntry.BuildSemiColonStrings();

                        _issues.Add(newEntry);
                    }

                    requestFinishedCallback(_issues);
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
                    List<BugReporterPlugin.UserEntry> entries = new List<BugReporterPlugin.UserEntry>();
                    Debug.LogFormat("Found {0} users in that project", usersData.Length);
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
            public string title;
            public string body;

            public GithubUserData user;
            public GithubUserData[] assignees;
        }

        [System.Serializable]
        public class GithubUserData
        {
            public string login;
            public int id;
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