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
    internal sealed class ProductNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<LoupeCredentials>(credentialName);

            var client = new LoupeRestClient(credentials.BaseUrl, credentials.UserName, credentials.Password, null);
            string tenant = AH.NullIf(config["Tenant"], string.Empty);
            var applications = await client.GetApplicationsAsync(tenant).ConfigureAwait(false);

            return applications.data.Select(a => a.productName).Distinct().OrderBy(t => t);
        }
    }
}
