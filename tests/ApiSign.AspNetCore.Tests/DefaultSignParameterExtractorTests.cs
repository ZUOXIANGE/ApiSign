using System.Text;

using ApiSign.AspNetCore.Core;
using ApiSign.AspNetCore.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApiSign.AspNetCore.Tests;

public sealed class DefaultSignParameterExtractorTests
{
    [Fact]
    public async Task ExtractAsync_UsesPriority_QueryThenBodyThenHeaders()
    {
        var options = Options.Create(new ApiSignOptions());
        var extractor = new DefaultSignParameterExtractor(options, NullLogger<DefaultSignParameterExtractor>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.QueryString = new QueryString("?appId=query-app&timestamp=1710000000&biz=query");
        context.Request.Headers["appId"] = "header-app";
        context.Request.Headers["nonce"] = "header-nonce";
        context.Request.Headers["sign"] = "header-sign";
        context.Request.ContentType = "application/json";
        var json = "{\"appId\":\"body-app\",\"nonce\":\"body-nonce\",\"amount\":99}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Request.ContentLength = context.Request.Body.Length;

        var result = await extractor.ExtractAsync(context.Request);

        Assert.Equal("query-app", result.AppId);
        Assert.Equal("body-nonce", result.Nonce);
        Assert.Equal(1710000000, result.Timestamp);
        Assert.Equal("header-sign", result.Sign);
        Assert.Equal("query", result.OtherParams["biz"]);
        Assert.Equal("99", result.OtherParams["amount"]);
    }
}