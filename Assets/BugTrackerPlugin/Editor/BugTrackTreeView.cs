using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BugReporter;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public class BugTrackTreeView : TreeView
{
    private readonly float BUTTON_ROW_HEIGHT = EditorGUIUtility.singleLineHeight + 4;

    private GUIStyle _descriptionStyle;
    private BugTrackerWindow _bugTrackerWindow;

    public BugTrackTreeView(TreeViewState state) : base(state)
    {
        Setup();
    }

    public BugTrackTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
    {
        Setup();
    }

    public void SetOwner(BugTrackerWindow owner)
    {
        _bugTrackerWindow = owner;
    }

    void Setup()
    {
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        cellMargin = 6;

        extraSpaceBeforeIconAndLabel = 20;
        columnIndexForTreeFoldouts = 1;

        multiColumnHeader.sortingChanged += OnSortingChanged;
        multiColumnHeader.ResizeToFit();

        _descriptionStyle = new GUIStyle("box");
        _descriptionStyle.alignment = TextAnchor.LowerLeft;
        _descriptionStyle.richText = true;
        _descriptionStyle.wordWrap = true;
    }

    void OnSortingChanged(MultiColumnHeader multiColumnHeader)
    {
        Sort(GetRows());
        Repaint();
    }

    void Sort(IList<TreeViewItem> rows)
    {
        if (multiColumnHeader.sortedColumnIndex == -1)
            return;

        if (rows.Count == 0)
            return;

        int sortedColumn = multiColumnHeader.sortedColumnIndex;
        var childrens = rootItem.children.Cast<BugTrackerTreeItem>();


        var ordered = multiColumnHeader.IsSortedAscending(sortedColumn) ? childrens.OrderBy(k => GetKeyToCompare(k, sortedColumn)) : childrens.OrderByDescending(k => GetKeyToCompare(k, sortedColumn));

        rows.Clear();
        foreach (var v in ordered)
            rows.Add(v);
    }

    object GetKeyToCompare(BugTrackerTreeItem item, int column)
    {
        switch (column)
        {
            case 1:
                return item.entry.title;
            case 2:
                return item.entry.assigneesString;
            case 3:
                return item.entry.labelsString;
            case 4:
                return item.entry.severity;
            default:
                break;
        }

        return null;
    }

    protected override float GetCustomRowHeight(int row, TreeViewItem item)
    {
        BugTrackerTreeItem itm = item as BugTrackerTreeItem;
        if (itm.isDescription)
        {
            BugReporterPlugin.IssueEntry issue = BugReporterPlugin.issues[itm.issueID];
            float h = _descriptionStyle.CalcHeight(new GUIContent(issue.description), Screen.width);
            //add space for a row of buttons
            h += BUTTON_ROW_HEIGHT;
            return h;
        }

        return base.GetCustomRowHeight(row, item);
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        BugTrackerTreeItem itm = args.item as BugTrackerTreeItem;
        BugReporterPlugin.IssueEntry issue = itm.entry;

        if (!itm.isDescription)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                float indent = GetContentIndent(itm);
                Rect r = args.GetCellRect(i); 
                int column = args.GetColumn(i);
                int idx = column;

                switch (idx)
                {
                    case 0:
                        if (_bugTrackerWindow.IsCurrentSceneIssue(issue) && GUI.Button(r, "Go"))
                        {
                            _bugTrackerWindow.GoToIssue(issue);
                        }
                        break;
                    case 1:

                        r.x += indent;
                        r.width -= indent;
                        EditorGUI.LabelField(r, issue.title);
                        break;
                    case 2:
                        EditorGUI.LabelField(r, issue.assigneesString);
                        break;
                    case 3:
                        EditorGUI.LabelField(r, issue.labelsString);
                        break;
                    case 4:
                        EditorGUI.LabelField(r, issue.severity.ToString());
                        break;
                    default:
                        break;
                }
            }
        }
        else
        {
            Rect r = args.rowRect;
            r.height -= BUTTON_ROW_HEIGHT;
            EditorGUI.LabelField(r, issue.description, _descriptionStyle);

            r.y += r.height;
            r.height = BUTTON_ROW_HEIGHT;

            r.width = 200;
            if(GUI.Button(r, "Open in Browser"))
            {
                Application.OpenURL(issue.webUrl);
            }

            r.x += r.width;
            if (EditorGUIUtility.systemCopyBuffer == issue.webUrl)
            {
                EditorGUI.LabelField(r, "Url copied");
            }
            else if(GUI.Button(r, "Copy Url"))
            {
                EditorGUIUtility.systemCopyBuffer = issue.webUrl;
            }
        }
    }

    protected override TreeViewItem BuildRoot()
    {
        TreeViewItem root = new TreeViewItem();

        root.depth = -1;
        root.id = -1;
        root.parent = null;
        root.children = new List<TreeViewItem>();

        int uniqueId = 0;

        for (int i = 0; i < BugReporterPlugin.issues.Count; ++i)
        {
            BugTrackerTreeItem itm = new BugTrackerTreeItem(uniqueId);
            uniqueId += 1;

            itm.depth = 0;
            itm.issueID = i;
            itm.entry = BugReporterPlugin.issues[i];
            itm.isDescription = false;

            if (BugReporterPlugin.issues[i].description != "")
            {
                BugTrackerTreeItem descItm = new BugTrackerTreeItem(uniqueId);
                uniqueId += 1;

                descItm.depth = 1;
                descItm.issueID = i;
                descItm.entry = itm.entry;
                descItm.isDescription = true;

                itm.AddChild(descItm);
            }

            root.AddChild(itm);
        }

        return root;
    }

    protected override bool CanMultiSelect(TreeViewItem item)
    {
        return false;
    }

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        var itm = FindItem(selectedIds[0], rootItem) as BugTrackerTreeItem;

        if (itm.isDescription)
        {
            SetSelection(new int[] {itm.parent.id});
            _bugTrackerWindow.OpenIssue(itm.issueID);
        }
        else
        {
            _bugTrackerWindow.OpenIssue(itm.issueID);
        }
    }

    public void SelectIssue(int issueID)
    {
        var foundItm = rootItem.children.Find(itm => { return (itm as BugTrackerTreeItem).issueID == issueID; });

        if (foundItm != null)
        {
            //set selection does not seem to trigger a SelectionChanged callback, so we open the issue too instead of realying on SelectionChanged doing it
            SetSelection(new int[] {foundItm.id});
            _bugTrackerWindow.OpenIssue(issueID);
            Repaint();
        }
    }
}

public class BugTrackerTreeItem : TreeViewItem
{
    public BugReporterPlugin.IssueEntry entry;
    public int issueID;
    public bool isDescription;

    public BugTrackerTreeItem(int id) : base(id)
    {
        
    }
}