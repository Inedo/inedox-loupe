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
using Inedo.Extensions.Loupe.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Loupe.IssueSources
{
    [DisplayName("Loupe Issue Source")]
    [Description("Issue source for Loupe.")]
    public sealed class LoupeIssueSource : IssueSource, IHasCredentials<LoupeCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Persistent]
        [DisplayName("Tenant")]
        [PlaceholderText("Use tenant from credentials")]
        [SuggestableValue(typeof(TenantNameSuggestionProvider))]
        public string Tenant { get; set; }

        [Persistent]
        [Required]
        [DisplayName("Product")]
        [SuggestableValue(typeof(ProductNameSuggestionProvider))]
        public string Product { get; set; }

        [Persistent]
        [Required]
        [DisplayName("Application")]
        [SuggestableValue(typeof(ApplicationNameSuggestionProvider))]
        public string Application { get; set; }

        [Persistent]
        [DisplayName("Application version")]
        [PlaceholderText("$ReleaseNumber")]
        [Description("The application version may contain wildcards. If so, all issues associated with matching application versions in Loupe will be returned.")]
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

            var result = from i in issues
                         select new LoupeIssue(credentials.BaseUrl, i);

            return result;
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
