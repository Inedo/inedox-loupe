﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by Visual Studio via: 
//     Edit > Paste Special > Paste JSON as Classes
// </auto-generated>
//------------------------------------------------------------------------------

using System;

namespace Inedo.Extensions.Loupe.Client.Model
{
    public class IssuesForApplicationsResponse
    {
        public Issue[] data { get; set; }
        public int total { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }
        public object timeStamp { get; set; }
        public DateTime timeStampDisplay { get; set; }
    }

    public class Issue
    {
        public string id { get; set; }
        public Caption caption { get; set; }
        public string status { get; set; }
        public Addedby addedBy { get; set; }
        public DateTime addedOn { get; set; }
        public Updatedby updatedBy { get; set; }
        public DateTime updatedOn { get; set; }
        public DateTime lastOccurredOn { get; set; }
        public Assignedto assignedTo { get; set; }
        public int endpoints { get; set; }
        public int sessions { get; set; }
        public int occurrences { get; set; }
        public int users { get; set; }
        public object fixedInVersion { get; set; }
        public string productName { get; set; }
        public string applicationName { get; set; }
        public bool selectable { get; set; }
    }

    public class Caption
    {
        public string status { get; set; }
        public bool isSuppressed { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string id { get; set; }
        public object productName { get; set; }
        public object applicationName { get; set; }
    }

    public class Addedby
    {
        public Email email { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string id { get; set; }
        public object productName { get; set; }
        public object applicationName { get; set; }
    }

    public class Email
    {
        public string address { get; set; }
        public string hash { get; set; }
    }

    public class Updatedby
    {
        public Email email { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string id { get; set; }
        public object productName { get; set; }
        public object applicationName { get; set; }
    }

    public class Assignedto
    {
        public Email email { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public string id { get; set; }
        public object productName { get; set; }
        public object applicationName { get; set; }
    }
}
