using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BugReporter
{
    //Subclass that to add a new backend to the system. See GithubBackend for example of implementation.
    public abstract class BugReporterBackend
    {
        //All those should be filed by the backend
        protected List<BugReporterPlugin.UserEntry> _users = new List<BugReporterPlugin.UserEntry>();
        protected List<BugReporterPlugin.IssueEntry> _issues = new List<BugReporterPlugin.IssueEntry>();
        protected List<string> _labels = new List<string>();

        public List<BugReporterPlugin.UserEntry> users { get { return _users; } }
        public List<BugReporterPlugin.IssueEntry> issues { get { return _issues; } }
        public List<string> labels { get { return _labels; } }

        //This will only be called once after init is done. You should check if "CanRequest()" return true first.
        //If it does, you can directly do what you need, otherwise, register to that callback.
        public System.Action OnPostInit;

        /// <summary>
        /// Called once the backend was created. Use that to ask for credential and test connection to the service
        /// </summary>
        public abstract void Init();
        /// <summary>
        /// Called by the system when the user set the project path they want to use (e.g. for Github user/project path).
        /// </summary>
        /// <param name="projectPath"></param>
        public abstract void SetProjectPath(string projectPath);
        /// <summary>
        /// Should return true if the abckend is ready to accept query, false otherwise.
        /// </summary>
        /// <returns></returns>
        public abstract bool CanRequest();
        /// <summary>
        /// Return the name of this backend, used to identify it in the settings & backend selection drop down
        /// </summary>
        /// <returns></returns>
        public abstract string GetName();
        /// <summary>
        /// This should return true if the issue tracker have a severity/priority system
        /// Will return as out parameters the scale (0 to max) and the default value it should assign to new issues.
        /// </summary>
        /// <param name="count"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public abstract bool UsePriority(out int max, out int defaultValue);
        /// <summary>
        /// Called by the system when need to recover issues/bug. It should call requestFinishedCallback once all issue are reconvered.
        /// </summary>
        /// <param name="requestFinishedCallback"></param>
        /// <param name="filter">An additional filter, to filter for a specific user and/or a set of labels</param>
        public abstract void RequestIssues(System.Action<List<BugReporterPlugin.IssueEntry>> requestFinishedCallback, BugReporterPlugin.IssueFilter filter);
        /// <summary>
        /// Called by the system when the users have logged as issue. All data should be sent to the backend.
        /// Note : the unitybt url will be part of the description at that point if the user asked to log position.
        /// </summary>
        /// <param name="issue"></param>
        public abstract void LogIssue(BugReporterPlugin.IssueEntry issue);
    }
}
