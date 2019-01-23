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

            this.log.LogDebug($"Searching for version {version}...");

            var versionData = versions.data.FirstOrDefault(v => string.Equals(v.version.title, version, StringComparison.OrdinalIgnoreCase));
            if (versionData == null)
                throw new LoupeRestException(404, $"version '{version}' not found in Loupe.", null);

            IssuesForApplicationsResponse openIssues;
            try
            {
                openIssues = await this.InvokeAsync<IssuesForApplicationsResponse>(
                    token,
                    "GET",
                    "Issues/OpenForApplication",
                    new LoupeApiOptions
                    {
                        Tenant = tenant,
                        IncludeQueryString = true,
                        ApplicationVersionId = versionData.id
                    }
                ).ConfigureAwait(false);
            }
            catch (LoupeRestException ex) when (ex.StatusCode == 404)
            {
                // a 404 on this endpoint means there are no issues, simulate that response
                openIssues = new IssuesForApplicationsResponse { data = new Issue[0] };
            }

            IssuesForApplicationsResponse closedIssues;
            try
            { 
                closedIssues = await this.InvokeAsync<IssuesForApplicationsResponse>(
                    token,
                    "GET",
                    "Issues/ClosedForApplication",
                    new LoupeApiOptions
                    {
                        Tenant = tenant,
                        IncludeQueryString = true,
                        ApplicationVersionId = versionData.id
                    }
                ).ConfigureAwait(false);
            }
            catch (LoupeRestException ex) when (ex.StatusCode == 404)
            {
                // a 404 on this endpoint means there are no issues, simulate that response
                closedIssues = new IssuesForApplicationsResponse { data = new Issue[0] };
            }

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
            {
                request.Headers.Add("loupe-product", arguments.Product);
                this.log.LogDebug("Filtering by product: " + arguments.Product);
            }
            if (arguments.Application != null)
            {
                request.Headers.Add("loupe-application", arguments.Application);
                this.log.LogDebug("Filtering by application: " + arguments.Application);
            }

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
                throw LoupeRestException.Wrap(ex, url);
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
                throw LoupeRestException.Wrap(ex, url);
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

    internal sealed class LoupeRestException : Exception
    {
        public LoupeRestException(int statusCode, string message, Exception inner)
            : base(message, inner)
        {
            this.StatusCode = statusCode;
        }

        public int StatusCode { get; }

        public string FullMessage => $"The server returned an error ({this.StatusCode}): {this.Message}";

        public static LoupeRestException Wrap(WebException ex, string url)
        {
            var response = (HttpWebResponse)ex.Response;
            try
            {
                var error = LoupeRestClient.DeserializeJson<Error>(response);
                return new LoupeRestException((int)response.StatusCode, error.message, ex);
            }
            catch
            {
                using (var responseStream = ex.Response.GetResponseStream())
                {
                    try
                    {
                        string errorText = new StreamReader(responseStream).ReadToEnd();
                        return new LoupeRestException((int)response.StatusCode, errorText, ex);
                    }
                    catch
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                            return new LoupeRestException((int)response.StatusCode, "Verify that the credentials used to connect are correct.", ex);
                        if (response.StatusCode == HttpStatusCode.Forbidden)
                            return new LoupeRestException((int)response.StatusCode, "Verify that the credentials used to connect have permission to access related resources.", ex);
                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return new LoupeRestException((int)response.StatusCode, $"Verify that the URL in the operation or credentials is correct (resolved to '{url}').", ex);

                        throw ex;
                    }
                }
            }
        }
    }
}
