using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using BugReporter;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class BugTrackerWindow : EditorWindow
{
    private readonly string bugIconeBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAABGdBTUEAALGPC/xhBQAAAAlwSFlzAAAOwgAADsIBFShKgAAAABl0RVh0U29mdHdhcmUAcGFpbnQubmV0IDQuMC4xOdTWsmQAAADiSURBVFhH7Y7BDsMgDEPZbf//w12MShUCSROg3QVLTy0QO04RHd90eDjH10mEm4jZOYmwEMIb1md0sSRcohi05fy9x52Hzra0kIIVMuPNwsDTBVQ/ezRRA0hefzfDYwazBUCTgYu3C1Q5XiNYUQBcOfj5V4E8HzGBVQXALrAL7ALDBSx6Ho08P2rs0ZvVYB46BQIqI1MkA1QZEXNlFIpkNDlvF6BvLVx6AmYLsD2t2KOKaiZ5vJY/6+kC9LWFoQKOEv7e485DZ5+swAjhxVzFPFJEeOckwkzE7FrxcItz3KGUflKLidBynYcNAAAAAElFTkSuQmCC";

    private readonly string bugIconeSelectedBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAZdEVYdFNvZnR3YXJlAHBhaW50Lm5ldCA0LjAuMTnU1rJkAAABS0lEQVRYR62UAW6DMBAEqdRX9f//IqzPpo6ZBfuSkUaqyg4maZNtkX3Sr3PefP+9t98efsx5Mzrszr49XObncP9LHDyq+1SnKYE7vF13TjSP4E2auh4zBJumrsfMU0YUN+vGgU1T16tIuUhhb905sOnVpnoBg1HtYo5gM6pdzP+Z/o/XNhIEm1Htqic4JLWNBMGG1DaS44eVz7v2kSHYkDpT+6VItsiAjVP7dGTAxql9OjJg49Q+HRmwcWqfjgzYOLVPRwZsnNqnIwM2Tu2Xo+7ze3Hl+0TWLh1epK2z6wrTD9F/gw0svRDtIwvKL2g4WrcObEbdi8DxqHYxR7AZ1S7m70y9hdrFHMGm9+ZPWHh8CG1iimDTfDq8gXFT12OGYNPU9ZjdU4bunehexcXJZhp7wxW7w1OUOPMg2VftmH6Qbx880t/8zkm27QURUshXRlt6awAAAABJRU5ErkJggg==";

    private Vector2 scrollPosition = Vector2.zero;
    private GUIContent iconeContent, iconeSelectedContent;

    private int _currentOpenEntry = -1;

    private BugReporterPlugin.IssueFilter _filter = new BugReporterPlugin.IssueFilter();

    private BugTrackTreeView _treeView;
    private Rect _lastRect = new Rect();

    private List<BugReporterPlugin.IssueEntry> _currentLevelsIssues = new List<BugReporterPlugin.IssueEntry>();

    [MenuItem("Bug Tracker/Open")]
    static void Open()
    {
        GetWindow<BugTrackerWindow>();
    }

    private void OnEnable()
    {
        BugReporterPlugin.Init();

        byte[] data = System.Convert.FromBase64String(bugIconeBase64);
        Texture2D texture = new Texture2D(1,1);
        texture.LoadImage(data);

        iconeContent = new GUIContent(texture);

        data = System.Convert.FromBase64String(bugIconeSelectedBase64);
        texture = new Texture2D(1, 1);
        texture.LoadImage(data);
        iconeSelectedContent = new GUIContent(texture);

        BuildTreeView();

        SceneView.onSceneGUIDelegate += SceneGUI;
    }

    private void OnDisable()
    {
        BugReporterPlugin.SaveSettings();
        SceneView.onSceneGUIDelegate -= SceneGUI;
        SceneView.RepaintAll();
    }

    private void BuildTreeView()
    {
        TreeViewState state = new TreeViewState();

        MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[5];

        columns[0] = new MultiColumnHeaderState.Column();
        columns[0].headerContent = new GUIContent("Go");
        columns[0].width = 50;
        columns[0].autoResize = false;
        columns[0].minWidth = 50;
        columns[0].maxWidth = 50;
        columns[0].canSort = false;

        columns[1] = new MultiColumnHeaderState.Column();
        columns[1].headerContent = new GUIContent("Title");
        columns[1].width = 100;
        columns[1].canSort = true;

        columns[2] = new MultiColumnHeaderState.Column();
        columns[2].headerContent = new GUIContent("Assignees");
        columns[2].width = 100;
        columns[2].canSort = true;

        columns[3] = new MultiColumnHeaderState.Column();
        columns[3].headerContent = new GUIContent("Labels");
        columns[3].width = 100;
        columns[3].canSort = true;

        columns[4] = new MultiColumnHeaderState.Column();
        columns[4].headerContent = new GUIContent("Severity");
        columns[4].width = 100;
        columns[4].canSort = true;

        MultiColumnHeaderState headerstate = new MultiColumnHeaderState(columns);
        MultiColumnHeader header = new MultiColumnHeader(headerstate);

        _treeView = new BugTrackTreeView(state, header);
        _treeView.SetOwner(this);
        _treeView.Reload();
    }

    void ReceivedIssues(List<BugReporterPlugin.IssueEntry> entries)
    {
        _treeView.Reload();

        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        string[] loadedSceneGUID = new string[sceneCount];
        for (int i = 0; i < sceneCount; ++i)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            loadedSceneGUID[i] = AssetDatabase.AssetPathToGUID(scene.path);
        }


        _currentOpenEntry = -1;

        _currentLevelsIssues.Clear();
        for (int i = 0; i < entries.Count; ++i)
        {
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

    public bool IsCurrentSceneIssue(BugReporterPlugin.IssueEntry issue)
    {
        return issue.unityBTURL != "" && _currentLevelsIssues.Contains(issue);
    }

    public void GoToIssue(BugReporterPlugin.IssueEntry issue)
    {
        var sceneView = GetWindow<SceneView>();
        sceneView.LookAt(issue.cameraPosition, issue.cameraRotation, issue.cameraDistance);
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

                if (BugReporterPlugin.issueRequestState == BugReporterPlugin.IssueRequestState.Requesting)
                {
                    EditorGUILayout.LabelField("LOADING ISSUES...");
                }

                if (Event.current.type == EventType.Repaint)
                    _lastRect = GUILayoutUtility.GetLastRect();

                float y = _lastRect.y + _lastRect.height;
                Rect r = new Rect(0, y, position.width, position.height - y);
                _treeView.OnGUI(r);
            }
        }
    }

    public void OpenIssue(int issue)
    {
        _currentOpenEntry = issue;
        SceneView.RepaintAll();
    }

    void SceneGUI(SceneView view)
    {
        Handles.BeginGUI();
        for (int i = 0; i < _currentLevelsIssues.Count; ++i)
        {
            var issue = _currentLevelsIssues[i];
            Vector3 position = issue.cameraPosition - issue.cameraRotation * new Vector3(0, 0, issue.cameraDistance);


            bool isCurrentIssue = _currentOpenEntry != -1 && BugReporterPlugin.issues[_currentOpenEntry] == issue;
            GUIContent currentContent = isCurrentIssue ? iconeSelectedContent : iconeContent;
            Rect guiPosition = HandleUtility.WorldPointToSizedRect(position, currentContent, GUI.skin.label);

            float size = 1.0f / HandleUtility.GetHandleSize(position);

            guiPosition.width *= size;
            guiPosition.height *= size;

            if (GUI.Button(guiPosition, currentContent, "label"))
            {
                //TODO :  really I need to stop searching stuff in list and build lookup table...
                _treeView.SelectIssue(BugReporterPlugin.issues.FindIndex(a => a == issue));
            }
        }

        Handles.EndGUI();
    }
}
