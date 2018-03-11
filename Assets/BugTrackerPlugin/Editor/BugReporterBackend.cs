using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugReporter
{
    public abstract class BugReporterBackend
    {
        protected List<BugReporterPlugin.UserEntry> _users = new List<BugReporterPlugin.UserEntry>();
        protected List<BugReporterPlugin.IssueEntry> _issues = new List<BugReporterPlugin.IssueEntry>();
        protected List<string> _labels = new List<string>();

        public List<BugReporterPlugin.UserEntry> users { get { return _users; } }
        public List<BugReporterPlugin.IssueEntry> issues { get { return _issues; } }
        public List<string> labels { get { return _labels; } }

        //This will only be called once after init is done. You should check if "CanRequest()" return true first.
        //If it does, you can directly do what you need, otherwise, register to that callback.
        public System.Action OnPostInit;

        public abstract bool Init();
        public abstract void SetProjectPath(string projectPath);
        public abstract bool CanRequest();
        public abstract string GetName();
        public abstract void RequestIssues(System.Action<List<BugReporterPlugin.IssueEntry>> requestFinishedCallback, BugReporterPlugin.IssueFilter filter);
        public abstract void LogIssue(BugReporterPlugin.IssueEntry issue);

        public abstract BugReporterPlugin.UserEntry GetCurrentUserInfo();
        public abstract BugReporterPlugin.UserEntry GetUserInfoByID(string userID);
        public abstract BugReporterPlugin.UserEntry GetUserInfoByName(string username);
    }
}
