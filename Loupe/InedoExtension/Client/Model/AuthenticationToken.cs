using Newtonsoft.Json;

namespace Inedo.Extensions.Loupe.Client.Model
{
    internal sealed class AuthenticationToken
    {
        [JsonProperty("access_token")]
        public string Token { get; set; }

        [JsonProperty("expires_in")]
        public long ExpiresIn { get; set; }
    }
}
