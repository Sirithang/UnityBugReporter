using System.Collections;
using System.Collections.Generic;
using BugReporter;
using UnityEditor;
using UnityEngine;

public class BugReporterWindow : EditorWindow
{
    private Vector2 scrollPosition = Vector2.zero;

    [MenuItem("Bug Reporter/Open")]
    static void Open()
    {
        GetWindow<BugReporterWindow>();
    }

    private void OnEnable()
    {
        BugReporterPlugin.Init();
    }

    private void OnDisable()
    {
        BugReporterPlugin.SaveSettings();
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
                if (GUILayout.Button("Change project path"))
                    backendSetting.projectPath = "";

                if (BugReporterPlugin.backend.CanRequest())
                {
                    if (BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Empty)
                        BugReporterPlugin.RequestIssues();
                    else if (BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Requesting)
                    {
                        EditorGUILayout.LabelField("LOADING ISSUES...");
                    }
                    else
                    {
                        //TODO : make that once and just register to scene laoded/unloaded
                        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
                        string[] loadedSceneGUID = new string[sceneCount];
                        for(int i = 0; i < sceneCount; ++i)
                        {
                            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                            loadedSceneGUID[i] = AssetDatabase.AssetPathToGUID(scene.path);
                        }

                        EditorGUILayout.BeginScrollView(scrollPosition);

                        //TODO : this is temp, replace with actual UI
                        for (int i = 0; i < BugReporterPlugin.issues.Count; ++i)
                        {
                            var issue = BugReporterPlugin.issues[i];
                            EditorGUILayout.BeginVertical("box");
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(issue.title, new GUIStyle("box"));
                            if (issue.unityBTURL != "" && ArrayUtility.Contains(loadedSceneGUID, issue.sceneGUID) && GUILayout.Button("Go To"))
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
}
