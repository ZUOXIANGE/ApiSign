using ApiSign.AspNetCore.Models;

namespace ApiSign.AspNetCore.Abstractions;

/// <summary>
/// Calculates request signatures.
/// </summary>
public interface ISignatureCalculator
{
    /// <summary>
    /// Builds the canonical string used for signature generation.
    /// </summary>
    string BuildCanonicalString(SignParameters parameters);

    /// <summary>
    /// Calculates the request signature using the selected algorithm.
    /// </summary>
    string Calculate(SignParameters parameters, string secretKey, SignAlgorithm algorithm);

    /// <summary>
    /// Calculates the request signature using a pre-computed canonical string.
    /// </summary>
    string Calculate(string canonicalString, string secretKey, SignAlgorithm algorithm);
}