using System.ComponentModel;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace ApiSign.AspNetCore.Models;

/// <summary>
/// Supported signature algorithms.
/// </summary>
public enum SignAlgorithm
{
    /// <summary>
    /// MD5.
    /// </summary>
    [Description("MD5")]
    MD5 = 1,

    /// <summary>
    /// SHA256.
    /// </summary>
    [Description("SHA256")]
    SHA256 = 2,

    /// <summary>
    /// HMAC-SHA256.
    /// </summary>
    [Description("HMAC-SHA256")]
    HMACSHA256 = 3,

    /// <summary>
    /// HMAC-SHA512.
    /// </summary>
    [Description("HMAC-SHA512")]
    HMACSHA512 = 4,
}