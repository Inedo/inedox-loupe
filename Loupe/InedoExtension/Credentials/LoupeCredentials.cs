using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Loupe.Client;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Loupe.Credentials
{
    [ScriptAlias("Loupe")]
    [DisplayName("Loupe")]
    [Description("Credentials for Loupe.")]
    public sealed class LoupeCredentials : ResourceCredentials
    {
        [Persistent]
        [DisplayName("API base URL")]
        [PlaceholderText(LoupeRestClient.DefaultBaseUrl)]
        public string BaseUrl { get; set; }

        [Persistent]
        [DisplayName("Tenant name")]
        [PlaceholderText("Single tenant")]
        [Description("Hosted Loupe should always supply a tenant, also known as a customer name. If you are self-hosting Loupe, then you may still be be multi-tenant, "
            + "although most likely your installation will be single tenant, in which case you can omit the tenant.")]
        public string Tenant { get; set; }

        [Persistent]
        [Required]
        [DisplayName("User name")]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [Required]
        [DisplayName("Password")]
        [FieldEditMode(FieldEditMode.Password)]
        public SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            var desc = new RichDescription(this.UserName);
            if (!string.IsNullOrEmpty(this.Tenant))
                desc.AppendContent($" ({this.Tenant})");
            return desc;
        }
    }
}
