using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BugReporter
{
    public class BugReportWindow : EditorWindow
    {
        public BugReporterPlugin.IssueEntry entry;
        public bool logPosition = true;
        public bool uploadScreenshot = true;

        public System.Action<BugReportWindow> onWindowClosed;

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

        void ToggleUserAssignee(object data)
        {
            BugReporterPlugin.UserEntry user = data as BugReporterPlugin.UserEntry;

            if (ArrayUtility.Contains(entry.assignees, user))
            {
                ArrayUtility.Remove(ref entry.assignees, user);
            }
            else
            {
                ArrayUtility.Add(ref entry.assignees, user);
            }

            entry.BuildCommaStrings();
        }

        void ToggleLabel(object data)
        {
            string label = data as string;

            if (ArrayUtility.Contains(entry.labels, label))
            {
                ArrayUtility.Remove(ref entry.labels, label);
            }
            else
            {
                ArrayUtility.Add(ref entry.labels, label);
            }

            entry.BuildCommaStrings();
        }

        private void OnGUI()
        {
            entry.title = EditorGUILayout.TextField("Title", entry.title);
            EditorGUILayout.LabelField("Description");
            entry.description = EditorGUILayout.TextArea(entry.description, GUILayout.MinHeight((150)));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Assignees");
            if (EditorGUILayout.DropdownButton(new GUIContent(entry.assigneesString), FocusType.Keyboard))
            {
                GenericMenu menu = new GenericMenu();

                var users = BugReporterPlugin.users;
                foreach (var user in users)
                {
                    menu.AddItem(new GUIContent(user.name), ArrayUtility.Contains(entry.assignees, user), ToggleUserAssignee, user);
                }

                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Labels");
            if (EditorGUILayout.DropdownButton(new GUIContent(entry.labelsString), FocusType.Keyboard))
            {
                GenericMenu menu = new GenericMenu();

                var labels = BugReporterPlugin.labels;
                foreach (var label in labels)
                {
                    menu.AddItem(new GUIContent(label), ArrayUtility.Contains(entry.labels, label), ToggleLabel, label);
                }

                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            logPosition = EditorGUILayout.Toggle("Log Position", logPosition);

            if (BugReporterPlugin.settings.currentImageUploader == "")
            {
                int selected = EditorGUILayout.Popup("Image uploader", -1, BugReporterPlugin.GetImageUploaderName());
                if (selected != -1)
                    BugReporterPlugin.SetupImageUploader(BugReporterPlugin.GetImageUploaderName()[selected]);
            }
            else
            {
                uploadScreenshot = EditorGUILayout.Toggle("Upload Screenshot", uploadScreenshot);
            }

            EditorGUILayout.EndHorizontal();

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
