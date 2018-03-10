using System.Collections;
using System.Collections.Generic;
using BugReporter;
using UnityEditor;
using UnityEngine;

public class BugReporterWindow : EditorWindow
{
    private Vector2 scrollPosition = Vector2.zero;
    GUIContent iconeContent;

    public List<BugReporterPlugin.IssueEntry> _currentLevelsIssues = new List<BugReporterPlugin.IssueEntry>();

    [MenuItem("Bug Reporter/Open")]
    static void Open()
    {
        GetWindow<BugReporterWindow>();
    }

    private void OnEnable()
    {
        BugReporterPlugin.Init();

        if(BugReporterPlugin.backend != null && BugReporterPlugin.backend.CanRequest())
        {
            BugReporterPlugin.RequestIssues(ReceivedIssues);
        }

        iconeContent = new GUIContent(EditorGUIUtility.Load("BugIcone.png") as Texture2D);

        SceneView.onSceneGUIDelegate += SceneGUI;
    }

    private void OnDisable()
    {
        BugReporterPlugin.SaveSettings();
        SceneView.onSceneGUIDelegate -= SceneGUI;
    }

    void ReceivedIssues(List<BugReporterPlugin.IssueEntry> entries)
    {
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        string[] loadedSceneGUID = new string[sceneCount];
        for (int i = 0; i < sceneCount; ++i)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            loadedSceneGUID[i] = AssetDatabase.AssetPathToGUID(scene.path);
        }

        _currentLevelsIssues.Clear();
        for (int i = 0; i < entries.Count; ++i)
        {
           if(ArrayUtility.Contains(loadedSceneGUID, entries[i].sceneGUID))
            {
                _currentLevelsIssues.Add(entries[i]);
            }
        }
    }

    private void OnGUI()
    {
        if (BugReporterPlugin.settings.currentBackendType == BugReporterPlugin.BackendType.None)
        {
            BugReporterPlugin.BackendType selected = (BugReporterPlugin.BackendType)EditorGUILayout.EnumPopup("Backend", BugReporterPlugin.settings.currentBackendType);
            if (selected != BugReporterPlugin.settings.currentBackendType)
            {
                BugReporterPlugin.SetupBackend(selected);
                BugReporterPlugin.SaveSettings();
            }
        }
        else
        {
            var backendSetting = BugReporterPlugin.settings.GetBackendSettings(BugReporterPlugin.backend.GetName());

            if (backendSetting.projectPath == "")
            {
                EditorGUI.BeginChangeCheck();
                backendSetting.projectPath =
                    EditorGUILayout.DelayedTextField("Enter project path", backendSetting.projectPath);
                if (EditorGUI.EndChangeCheck())
                {
                    
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Change project path"))
                    backendSetting.projectPath = "";
                if (BugReporterPlugin.backend.CanRequest() && GUILayout.Button("Referesh issues"))
                {
                    BugReporterPlugin.RequestIssues(ReceivedIssues);
                }
                EditorGUILayout.EndHorizontal();

                if (BugReporterPlugin.backend.CanRequest())
                {
                    if (BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Empty)
                        BugReporterPlugin.RequestIssues(ReceivedIssues);
                    else if (BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Requesting)
                    {
                        EditorGUILayout.LabelField("LOADING ISSUES...");
                    }
                    else
                    {
                        EditorGUILayout.BeginScrollView(scrollPosition);

                        //TODO : this is temp, replace with actual UI
                        for (int i = 0; i < BugReporterPlugin.issues.Count; ++i)
                        {
                            var issue = BugReporterPlugin.issues[i];
                            EditorGUILayout.BeginVertical("box");
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(issue.title, new GUIStyle("box"));
                            if (issue.unityBTURL != "" && _currentLevelsIssues.Contains(issue) && GUILayout.Button("Go To"))
                            {
                                var sceneView = GetWindow<SceneView>();
                                sceneView.LookAt(issue.cameraPosition, issue.cameraRotation, issue.cameraDistance);
                            }
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.LabelField(issue.description, new GUIStyle("box"));
                            EditorGUILayout.LabelField(issue.assignee, new GUIStyle("box"));
                            EditorGUILayout.EndVertical();
                        }

                        EditorGUILayout.EndScrollView();
                    }
                }
            }
        }
    }

    void SceneGUI(SceneView view)
    {
        for(int i = 0; i < _currentLevelsIssues.Count; ++i)
        {
            var issue = _currentLevelsIssues[i];
            Vector3 position = issue.cameraPosition - issue.cameraRotation * new Vector3(0, 0, issue.cameraDistance);

            Handles.Label(position, iconeContent);
        }
    }
}
