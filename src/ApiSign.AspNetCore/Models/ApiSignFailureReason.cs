using System.ComponentModel;

namespace ApiSign.AspNetCore.Models;

/// <summary>
/// Describes why signature validation failed.
/// </summary>
public enum ApiSignFailureReason
{
    /// <summary>
    /// No failure reason.
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// Missing parameters.
    /// </summary>
    [Description("MissingParameters")]
    MissingParameters = 1,

    /// <summary>
    /// Invalid timestamp.
    /// </summary>
    [Description("InvalidTimestamp")]
    InvalidTimestamp = 2,

    /// <summary>
    /// App not found.
    /// </summary>
    [Description("AppNotFound")]
    AppNotFound = 3,

    /// <summary>
    /// App disabled.
    /// </summary>
    [Description("AppDisabled")]
    AppDisabled = 4,

    /// <summary>
    /// Replay attack.
    /// </summary>
    [Description("ReplayAttack")]
    ReplayAttack = 5,

    /// <summary>
    /// Invalid signature.
    /// </summary>
    [Description("InvalidSignature")]
    InvalidSignature = 6,
}