namespace ApiSign.SampleWeb.Infrastructure;

public sealed class RedisNonceOptions
{
    public const string SectionName = "RedisNonce";

    public bool Enabled { get; set; }

    public string Configuration { get; set; } = "localhost:6379";

    public string InstanceName { get; set; } = "apisign:";
}