namespace VTSLegalOfficeAI.Options
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "VTSLegalOfficeAI";
        public string Audience { get; set; } = "VTSLegalOfficeAI";
        public int ExpiryMinutes { get; set; } = 480;
    }
}
