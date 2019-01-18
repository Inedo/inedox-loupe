using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Extensions.Loupe.Client.Model;
using Newtonsoft.Json;

namespace Inedo.Extensions.Loupe.Client
{
    internal sealed class LoupeRestClient
    {
        public const string DefaultBaseUrl = "https://us.onloupe.com";

        private readonly string baseUrl;
        private readonly string userName;
        private readonly SecureString password;
        private readonly ILogSink log;

        public LoupeRestClient(string baseUrl, string userName, SecureString password, ILogSink log)
        {
            this.baseUrl = AH.CoalesceString(baseUrl, DefaultBaseUrl).TrimEnd('/');
            this.userName = userName ?? throw new ArgumentNullException(nameof(userName));
            this.password = password ?? throw new ArgumentNullException(nameof(password));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task<ProductsAndApplicationsResponse> GetApplicationsAsync(string tenant)
        {
            var token = await this.AuthenticateAsync().ConfigureAwait(false);

            var result = await this.InvokeAsync<ProductsAndApplicationsResponse>(
                token,
                "GET",
                "Application/AllProductsAndApplications",
                new LoupeApiOptions { Tenant = tenant }
            ).ConfigureAwait(false);

            return result;
        }

        public async Task<ApplicationVersionResponse> GetVersionsAsync(string tenant)
        {
            var token = await this.AuthenticateAsync().ConfigureAwait(false);

            var result = await this.InvokeAsync<ApplicationVersionResponse>(
                token,
                "GET",
                "ApplicationVersion/Versions",
                new LoupeApiOptions
                {
                    Tenant = tenant,
                    IncludeQueryString = true,
                    ReleaseTypeId = Guid.Empty.ToString()
                }
            ).ConfigureAwait(false);

            return result;
        }

        public async Task<(IssuesForApplicationsResponse open, IssuesForApplicationsResponse closed)> GetIssuesAsync(string tenant, string version, string product = null, string application = null)
        {
            if (string.IsNullOrEmpty(version))
                throw new ArgumentNullException(nameof(version));

            var token = await this.AuthenticateAsync().ConfigureAwait(false);

            var versions = await this.InvokeAsync<ApplicationVersionResponse>(
                 token,
                 "GET",
                 "ApplicationVersion/Versions",
                 new LoupeApiOptions
                 {
                     Tenant = tenant,
                     IncludeQueryString = true,
                     ReleaseTypeId = Guid.Empty.ToString(),
                     Product = product,
                     Application = application
                 }
             ).ConfigureAwait(false);

            var versionData = versions.data.FirstOrDefault(v => string.Equals(v.version.title, version, StringComparison.OrdinalIgnoreCase));
            if (versionData == null)
                throw new ArgumentException($"version '{version}' not found in Loupe.");

            var openIssues = await this.InvokeAsync<IssuesForApplicationsResponse>(
                token,
                "GET",
                "Issues/OpenForApplications",
                new LoupeApiOptions
                {
                    Tenant = tenant,
                    IncludeQueryString = true,
                    ApplicationVersionId = versionData.id
                }
            ).ConfigureAwait(false);

            var closedIssues = await this.InvokeAsync<IssuesForApplicationsResponse>(
                token,
                "GET",
                "Issues/ClosedForApplications",
                new LoupeApiOptions
                {
                    Tenant = tenant,
                    IncludeQueryString = true,
                    ApplicationVersionId = versionData.id
                }
            ).ConfigureAwait(false);

            return (openIssues, closedIssues);
        }

        private async Task<T> InvokeAsync<T>(AuthenticationToken authToken, string method, string relativeUrl, LoupeApiOptions arguments, object data = null)
        {
            if (authToken == null)
                throw new ArgumentNullException(nameof(authToken));
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (relativeUrl == null)
                throw new ArgumentNullException(nameof(relativeUrl));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            string url = this.BuildApiUrl(relativeUrl, arguments);

            var request = WebRequest.Create(url);
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
                httpRequest.UserAgent = "InedoLoupeExtension/" + typeof(LoupeRestClient).Assembly.GetName().Version.ToString();

            request.Headers.Add("Authorization", "Session " + authToken.Token);

            if (arguments.Product != null)
                request.Headers.Add("loupe-product", arguments.Product);
            if (arguments.Application != null)
                request.Headers.Add("loupe-application", arguments.Application);

            request.ContentType = "application/json";
            request.Method = method;

            this.log.LogDebug($"Invoking Loupe REST API {request.Method} request ({request.ContentType}) to URL: {url}");

            if (data != null)
            {
                using (var requestStream = await request.GetRequestStreamAsync().ConfigureAwait(false))
                using (var writer = new StreamWriter(requestStream, InedoLib.UTF8Encoding))
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    var serializer = JsonSerializer.CreateDefault();
                    serializer.Serialize(jsonWriter, data);
                }
            }

            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                    return DeserializeJson<T>(response);
                }
            }
            catch (WebException ex) when (ex.Response != null)
            {
#warning FIX error handling
                throw;
            }
        }

        private string BuildApiUrl(string relativeUrl, LoupeApiOptions arguments)
        {
            string apiBaseUrl;
            if (arguments.Tenant != null)
                apiBaseUrl = $"{this.baseUrl}/Customers/{Uri.EscapeUriString(arguments.Tenant)}/api/";
            else
                apiBaseUrl = $"{this.baseUrl}/api/";

            string url = apiBaseUrl + relativeUrl + arguments.GetQueryString();
            return url;
        }

        private async Task<AuthenticationToken> AuthenticateAsync()
        {
            string url = this.BuildApiUrl("auth/token", LoupeApiOptions.Default);

            var request = WebRequest.Create(url);
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
                httpRequest.UserAgent = "InedoLoupeExtension/" + typeof(LoupeRestClient).Assembly.GetName().Version.ToString();
            request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(InedoLib.UTF8Encoding.GetBytes(this.userName + ":" + AH.Unprotect(this.password))));
            request.ContentType = "application/json";
            request.Method = "GET";

            this.log.LogDebug($"Invoking Loupe REST API {request.Method} request ({request.ContentType}) to URL: {url}");

            try
            {
                using (var response = await request.GetResponseAsync().ConfigureAwait(false))
                {
                    return DeserializeJson<AuthenticationToken>(response);
                }
            }
            catch (WebException ex) when (ex.Response != null)
            {
#warning FIX error handling
                throw;
            }
        }

        internal static T DeserializeJson<T>(WebResponse response)
        {
            using (var responseStream = response.GetResponseStream())
            using (var reader = new StreamReader(responseStream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var js = JsonSerializer.CreateDefault();
                return js.Deserialize<T>(jsonReader);
            }
        }
    }

    internal sealed class LoupeApiOptions
    {
        public static readonly LoupeApiOptions Default = new LoupeApiOptions();

        public string Tenant { get; set; }

        // Headers
        public string Product { get; set; }
        public string Application { get; set; }

        // Query string
        public bool IncludeQueryString { get; set; }
        public int Take { get; set; }
        public int Skip { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 500;
        public string SortKey { get; set; }
        public string SortDirection { get; set; }
        public string ReleaseTypeId { get; set; }
        public string ApplicationVersionId { get; set; }

        public string GetQueryString()
        {
            if (!this.IncludeQueryString)
                return string.Empty;

            var buffer = new StringBuilder(256);
            buffer.Append('?');
            buffer.AppendFormat("take={0}&", this.Take);
            buffer.AppendFormat("skip={0}&", this.Skip);
            buffer.AppendFormat("page={0}&", this.Page);
            buffer.AppendFormat("pageSize={0}&", this.PageSize);
            if (this.SortKey != null)
                buffer.AppendFormat("sortKey={0}&", this.SortKey);
            if (this.SortDirection != null)
                buffer.AppendFormat("sortDirection={0}&", this.SortDirection);
            if (this.ReleaseTypeId != null)
                buffer.AppendFormat("releaseTypeId={0}&", this.ReleaseTypeId);
            if (this.ApplicationVersionId != null)
                buffer.AppendFormat("applicationVersionId={0}&", this.ApplicationVersionId);

            return buffer.ToString().TrimEnd('?', '&');
        }
    }
}
