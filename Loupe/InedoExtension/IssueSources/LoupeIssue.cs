using System;
using Inedo.Extensibility.IssueSources;
using Inedo.Extensions.Loupe.Client;
using Inedo.Extensions.Loupe.Client.Model;

namespace Inedo.Extensions.Loupe.IssueSources
{
    public sealed class LoupeIssue : IIssueTrackerIssue
    {
        private readonly string baseUrl;
        private readonly Issue issue;

        public LoupeIssue(string baseUrl, Issue issue, bool closed)
        {
            this.baseUrl = (AH.CoalesceString(baseUrl, LoupeRestClient.DefaultBaseUrl)).TrimEnd('/');
            this.issue = issue ?? throw new ArgumentNullException(nameof(issue));
            this.IsClosed = closed || string.Equals(issue.status, "resolved", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsClosed { get; }

        public string Id => this.issue.caption.id;
        public string Status => this.issue.status;
        public string Type => null;
        public string Title => this.issue.caption.title;
        public string Description => null;
        public string Submitter => this.issue.addedBy.title;
        public DateTime SubmittedDate => this.issue.addedOn;
        public string Url => this.baseUrl + this.issue.caption.url;
    }
}
