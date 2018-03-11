using System.Collections;
using System.Collections.Generic;
using BugReporter;
using UnityEditor;
using UnityEngine;

public class BugTrackerWindow : EditorWindow
{
    private Vector2 scrollPosition = Vector2.zero;
    private GUIContent iconeContent, iconeSelectedContent;

    private int _currentOpenEntry = -1;
    private bool[] _foldoutInfos;

    private GUIStyle _entryHeaderStyle;
    private GUIStyle _entryDescriptionStyle;

    private BugReporterPlugin.IssueFilter _filter = new BugReporterPlugin.IssueFilter();

    private List<BugReporterPlugin.IssueEntry> _currentLevelsIssues = new List<BugReporterPlugin.IssueEntry>();

    [MenuItem("Bug Tracker/Open")]
    static void Open()
    {
        GetWindow<BugTrackerWindow>();
    }

    private void OnEnable()
    {
        _entryHeaderStyle = new GUIStyle("box");
        _entryHeaderStyle.stretchWidth = true;
        _entryHeaderStyle.alignment = TextAnchor.MiddleLeft;
        _entryHeaderStyle.padding = new RectOffset(2, 2, 2, 2);

        _entryDescriptionStyle = new GUIStyle(EditorStyles.textArea);

        BugReporterPlugin.Init();

        //if(BugReporterPlugin.backend != null && BugReporterPlugin.backend.CanRequest())
        //{
        //    BugReporterPlugin.RequestIssues(ReceivedIssues);
        //}

        iconeContent = new GUIContent(EditorGUIUtility.Load("BugIcone.png") as Texture2D);
        iconeSelectedContent = new GUIContent(EditorGUIUtility.Load("BugIcone_Selected.png") as Texture2D);

        SceneView.onSceneGUIDelegate += SceneGUI;
    }

    private void OnDisable()
    {
        BugReporterPlugin.SaveSettings();
        SceneView.onSceneGUIDelegate -= SceneGUI;
        SceneView.RepaintAll();
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


        _currentOpenEntry = -1;
        _foldoutInfos = new bool[entries.Count];

        _currentLevelsIssues.Clear();
        for (int i = 0; i < entries.Count; ++i)
        {
            _foldoutInfos[i] = false;

           if(ArrayUtility.Contains(loadedSceneGUID, entries[i].sceneGUID))
           {
               _currentLevelsIssues.Add(entries[i]);
           }
        }

        SceneView.RepaintAll();
        Repaint();
    }

    void ToggleUserFilter(object user)
    {
        _filter.user = user as BugReporterPlugin.UserEntry;
    }

    void ToggleLabelFilter(object data)
    {
        string label = data as string;

        if (ArrayUtility.Contains(_filter.labels, label))
        {
            ArrayUtility.Remove(ref _filter.labels, label);
        }
        else
        {
            ArrayUtility.Add(ref _filter.labels, label);
        }

        _filter.BuildLabelCommaString();
    }

    private void OnGUI()
    {
        if (BugReporterPlugin.settings.currentBackendType == "")
        {
            string[] backendNames = BugReporterPlugin.GetBackendNameList();
            int selected = EditorGUILayout.Popup("Backend", -1, backendNames);
            if (selected != -1)
            {
                BugReporterPlugin.SetupBackend(backendNames[selected]);
                BugReporterPlugin.SaveSettings();
            }
        }
        else
        {
            var backendSetting = BugReporterPlugin.settings.GetBackendSettings(BugReporterPlugin.backend.GetName());

            if (backendSetting.projectPath == "")
            {
                EditorGUI.BeginChangeCheck();
                string newPath =
                    EditorGUILayout.DelayedTextField("Enter project path", backendSetting.projectPath);
                if (EditorGUI.EndChangeCheck())
                {
                    BugReporterPlugin.SetProjectPath(newPath);
                }
            }
            else
            {
                if (GUILayout.Button("Change project path"))
                    backendSetting.projectPath = "";

                EditorGUILayout.PrefixLabel("Filters");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Assignee");
                if (EditorGUILayout.DropdownButton(new GUIContent(_filter.user != null ? _filter.user.name : "Anyone"), FocusType.Keyboard))
                {
                    GenericMenu menu = new GenericMenu();

                    menu.AddItem(new GUIContent("Anyone"), _filter.user == null, ToggleUserFilter, null);

                    var users = BugReporterPlugin.users;
                    foreach (var user in users)
                    {
                        menu.AddItem(new GUIContent(user.name), _filter.user == user, ToggleUserFilter, user);
                    }

                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Labels");
                if (EditorGUILayout.DropdownButton(new GUIContent(_filter.labelCommaString), FocusType.Keyboard))
                {
                    GenericMenu menu = new GenericMenu();

                    var labels = BugReporterPlugin.labels;
                    foreach (var label in labels)
                    {
                        menu.AddItem(new GUIContent(label), ArrayUtility.Contains(_filter.labels, label), ToggleLabelFilter, label);
                    }

                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();

                if (BugReporterPlugin.backend.CanRequest() && GUILayout.Button("Referesh issues"))
                {
                    BugReporterPlugin.RequestIssues(ReceivedIssues, _filter);
                }

                if (BugReporterPlugin.backend.CanRequest())
                {
                    if (BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Requesting)
                    {
                        EditorGUILayout.LabelField("LOADING ISSUES...");
                    }
                    else if(BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Completed)
                    {
                        EditorGUILayout.BeginScrollView(scrollPosition);

                        for (int i = 0; i < BugReporterPlugin.issues.Count; ++i)
                        {
                            var issue = BugReporterPlugin.issues[i];

                            bool canGoTo = issue.unityBTURL != "" && _currentLevelsIssues.Contains(issue);

                            if (canGoTo)
                                EditorGUILayout.BeginHorizontal();

                            if (GUILayout.Button(issue.title, _entryHeaderStyle))
                            {
                                if (_currentOpenEntry != -1) _foldoutInfos[_currentOpenEntry] = false;
                                _currentOpenEntry = i;
                                _foldoutInfos[i] = true;
                                SceneView.RepaintAll();
                            }

                            if (canGoTo)
                            {

                                if(GUILayout.Button("Go To", GUILayout.Width(64)))
                                {
                                    var sceneView = GetWindow<SceneView>();
                                    sceneView.LookAt(issue.cameraPosition, issue.cameraRotation, issue.cameraDistance);
                                }

                                EditorGUILayout.EndHorizontal();
                            }


                            if(_foldoutInfos[i])
                            {
                                EditorGUILayout.BeginVertical(_entryDescriptionStyle);
                                EditorGUILayout.LabelField(issue.description);

                                EditorGUILayout.Space();

                                string assigneesList = "Assignees : ";

                                if(issue.assignees.Length == 0)
                                {
                                    assigneesList += "None";
                                }
                                else
                                {
                                    assigneesList += issue.assigneesString;
                                }

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(assigneesList);
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.EndVertical();
                            }
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


            bool isCurrentIssue = _currentOpenEntry != -1 && BugReporterPlugin.issues[_currentOpenEntry] == issue;
            Handles.Label(position, isCurrentIssue ? iconeSelectedContent : iconeContent);
        }
    }
}
