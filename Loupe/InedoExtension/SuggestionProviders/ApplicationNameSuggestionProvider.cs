using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Loupe.Client;
using Inedo.Extensions.Loupe.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.Loupe.SuggestionProviders
{
    internal sealed class ApplicationNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<LoupeCredentials>(credentialName);

            var client = new LoupeRestClient(credentials.BaseUrl, credentials.UserName, credentials.Password, null);

            var applications = await client.GetApplicationsAsync(config["Tenant"]).ConfigureAwait(false);

            string product = AH.NullIf(config["Product"], string.Empty);

            return applications.data
                .Where(a => product == null || string.Equals(product, a.productName, System.StringComparison.OrdinalIgnoreCase))
                .Select(a => a.applicationName)
                .Distinct()
                .OrderBy(t => t);
        }
    }
}
