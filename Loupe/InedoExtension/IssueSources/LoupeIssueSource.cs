using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.IssueSources;
using Inedo.Extensions.Loupe.Client;
using Inedo.Extensions.Loupe.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Loupe.IssueSources
{
    [DisplayName("Loupe Issue Source")]
    [Description("Issue source for Loupe.")]
    public sealed class LoupeIssueSource : IssueSource, IHasCredentials<LoupeCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }
#warning FIX use suggestion provider
        [Persistent]
        [DisplayName("Tenant")]
        [PlaceholderText("Use tentant from credentials")]
        public string Tenant { get; set; }
#warning FIX use suggestion provider
        [Persistent]
        [Required]
        [DisplayName("Product")]
        public string Product { get; set; }
#warning FIX use suggestion provider
        [Persistent]
        [Required]
        [DisplayName("Application")]
        public string Application { get; set; }

        [Persistent]
        [DisplayName("Application version")]
        [PlaceholderText("$ReleaseNumber")]
        public string Version { get; set; } = "$ReleaseNumber";

        public override async Task<IEnumerable<IIssueTrackerIssue>> EnumerateIssuesAsync(IIssueSourceEnumerationContext context)
        {
            context.Log.LogDebug("Enumerating Loupe issue source...");

            var credentials = this.TryGetCredentials<LoupeCredentials>();

            var client = new LoupeRestClient(credentials.BaseUrl, credentials.UserName, credentials.Password, context.Log);

            var issues = await client.GetIssuesAsync(
                AH.CoalesceString(this.Tenant, credentials.Tenant),
                this.Version,
                this.Product,
                this.Application
            ).ConfigureAwait(false);

            var open = from i in issues.open.data
                       select new LoupeIssue(credentials.BaseUrl, i, false);
            var closed = from i in issues.closed.data
                         select new LoupeIssue(credentials.BaseUrl, i, true);

            return open.Concat(closed);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Get Issues from ",
                new Hilite(this.Product),
                " ",
                new Hilite(this.Application),
                " in Loupe Server."
            );
        }
    }
}
