using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Loupe.Client;
using Inedo.Extensions.Loupe.Credentials;
using Inedo.Extensions.Loupe.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.Loupe.Operations
{
    [ScriptAlias("Ensure-ApplicationVersion")]
    [DisplayName("Ensure Loupe Application Version")]
    [ScriptNamespace("Loupe", PreferUnqualified = false)]
    [Description("Ensures an application version exists and has the specified properties.")]
    [Example(@"# loop through all apps in the product and ensure the current release version exists
foreach $LoupeApp in @(BuildMaster Service, BuildMaster WebApp)
{
    Loupe::Ensure-ApplicationVersion
    (
        Credentials: Loupe,
        Product: BuildMaster,
        Application: $LoupeApp
    );
}
")]
    public sealed class EnsureApplicationVersionOperation : EnsureOperation, IHasCredentials<LoupeCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Required]
        [ScriptAlias("Product")]
        [DisplayName("Product")]
        [SuggestableValue(typeof(ProductNameSuggestionProvider))]
        public string Product { get; set; }

        [Required]
        [ScriptAlias("Application")]
        [DisplayName("Application")]
        [SuggestableValue(typeof(ApplicationNameSuggestionProvider))]
        public string Application { get; set; }

        [ScriptAlias("Version")]
        [DisplayName("Application version")]
        [DefaultValue("$ReleaseNumber")]
        public string Version { get; set; }

        [ScriptAlias("Caption")]
        [Description("When set to a value other than the application version, will render as: Caption Value (1.2.3.4)")]
        [DefaultValue("$ReleaseName")]
        public string Caption { get; set; }

        [ScriptAlias("Description")]
        [DisplayName("Description")]
        public string Description { get; set; }

        [ScriptAlias("PromotionLevel")]
        [DisplayName("Promotion level")]
        [SuggestableValue(typeof(PromotionLevelSuggestionProvider))]
        public string PromotionLevel { get; set; }

        [ScriptAlias("ReleaseNotesUrl")]
        [DisplayName("Release notes URL")]
        public string ReleaseNotesUrl { get; set; }

        [ScriptAlias("ReleaseDate")]
        [DisplayName("Release date")]
        public string ReleaseDate { get; set; }

        [ScriptAlias("ReleaseType")]
        [DisplayName("Release type")]
        [SuggestableValue(typeof(ReleaseTypeSuggestionProvider))]
        public string ReleaseType { get; set; }

        #region Duplicate credentials fields...
        [Category("Connection")]
        [ScriptAlias("BaseUrl")]
        [DisplayName("API base URL")]
        [PlaceholderText("Use default from credentials")]
        [MappedCredential(nameof(LoupeCredentials.BaseUrl))]
        public string BaseUrl { get; set; }

        [Category("Connection")]
        [ScriptAlias("Tenant")]
        [DisplayName("Tenant name")]
        [PlaceholderText("Use tenant from credentials")]
        [Description("Hosted Loupe should always supply a tenant, also known as a customer name. If you are self-hosting Loupe, then you may still be be multi-tenant, "
            + "although most likely your installation will be single tenant, in which case you can omit the tenant.")]
        [MappedCredential(nameof(LoupeCredentials.Tenant))]
        public string Tenant { get; set; }

        [Category("Connection")]
        [ScriptAlias("UserName")]
        [PlaceholderText("Use username from credentials")]
        [DisplayName("User name")]
        [MappedCredential(nameof(LoupeCredentials.UserName))]
        public string UserName { get; set; }

        [Category("Connection")]
        [ScriptAlias("Password")]
        [PlaceholderText("Use password from credentials")]
        [DisplayName("Password")]
        [FieldEditMode(FieldEditMode.Password)]
        [MappedCredential(nameof(LoupeCredentials.Password))]
        public SecureString Password { get; set; }
        #endregion

        public override async Task ConfigureAsync(IOperationExecutionContext context)
        {
            this.LogInformation($"Ensuring Loupe application version '{this.Version}' exists for product '{this.Product}', application '{this.Application}'...");

            var client = new LoupeRestClient(this.BaseUrl, this.UserName, this.Password, this);
            var version = await client.FindVersionAsync(this.Tenant, this.Version, this.Product, this.Application).ConfigureAwait(false);

            var options = new VersionOptions
            {
                Description = this.Description,
                Caption = this.Caption,
                DisplayVersion = this.Version,
                PromotionLevelCaption = this.PromotionLevel,
                ReleaseDate = AH.ParseDate(this.ReleaseDate),
                ReleaseNotesUrl = this.ReleaseNotesUrl,
                ReleaseTypeCaption = this.ReleaseType
            };

            this.LogDebug("Options: " + options);

            try
            {
                if (version == null)
                {
                    this.LogInformation("Version does not exist, creating...");

                    await client.CreateApplicationVersionAsync(this.Tenant, this.Product, this.Application, this.Version, options).ConfigureAwait(false);

                    this.LogInformation("Version created.");
                }
                else
                {
                    this.LogInformation("Version already exists, updating...");

                    await client.UpdateApplicationVersionAsync(this.Tenant, this.Product, this.Application, this.Version, options).ConfigureAwait(false);
                    this.LogInformation("Version updated.");
                }
            }
            catch (LoupeRestException ex)
            {
                this.LogError(ex.FullMessage);
            }
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
                new RichDescription("Ensure version ", new Hilite(config[nameof(this.Version)]), " exists "),
                new RichDescription("for product ", new Hilite(config[nameof(this.Product)]), "; application ", new Hilite(config[nameof(this.Application)]))
            );
        }
    }
}
