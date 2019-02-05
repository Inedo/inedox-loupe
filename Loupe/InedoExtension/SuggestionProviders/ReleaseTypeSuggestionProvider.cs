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
    internal sealed class ReleaseTypeSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<LoupeCredentials>(credentialName);

            var client = new LoupeRestClient(credentials.BaseUrl, credentials.UserName, credentials.Password, null);

            string tenant = AH.NullIf(AH.CoalesceString(config["Tenant"], credentials.Tenant), string.Empty);
            string product = AH.NullIf(config["Product"], string.Empty);
            string application = AH.NullIf(config["Application"], string.Empty);

            if (tenant == null || product == null || application == null)
                return Enumerable.Empty<string>();

            var types = await client.GetReleaseTypesAsync(tenant, product, application).ConfigureAwait(false);

            return types;
        }
    }
}
