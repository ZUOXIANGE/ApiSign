# ApiSign

[![NuGet](https://img.shields.io/nuget/v/ApiSign.AspNetCore)](https://www.nuget.org/packages/ApiSign.AspNetCore)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ApiSign.AspNetCore)](https://www.nuget.org/packages/ApiSign.AspNetCore)
[![CI](https://github.com/ZUOXIANGE/ApiSign/actions/workflows/ci.yml/badge.svg)](https://github.com/ZUOXIANGE/ApiSign/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://github.com/ZUOXIANGE/ApiSign/blob/main/LICENSE)

`ApiSign.AspNetCore` 是一个轻量级的 ASP.NET Core API 签名认证组件，专注请求防篡改、防重放和多算法签名验证。

## 功能

- 中间件和特性两种接入方式
- 时间戳有效期验证
- Nonce 防重放校验
- MD5、SHA256、HMACSHA256、HMACSHA512
- 可替换的密钥提供者、Nonce 存储和参数提取器
- 可自定义签名校验失败响应格式
- 默认内存 Nonce 存储和默认参数提取实现

## 快速开始

```csharp
builder.Services.AddSingleton<IAppSecretProvider, MyAppSecretProvider>();
builder.Services.AddApiSignAuthentication(options =>
{
    options.TimestampDisparitySeconds = 900;
    options.EnableNonce = true;
});

app.UseApiSignAuthentication(excludedPaths: new[] { "/health" });
```

签名字符串生成规则：

1. 将 `appId`、`nonce`、`timestamp` 与业务参数合并。
2. 排除 `sign` 字段，按参数名升序排序。
3. 对键和值做 URL Encode，拼成 `key=value&key2=value2`。
4. `MD5`/`SHA256` 使用 `canonical&key=secret` 摘要。
5. `HMACSHA256`/`HMACSHA512` 使用 `secret` 作为 HMAC key。

示例项目位于 `samples/ApiSign.SampleWeb`。

请求签名与调用示例见 `samples/ApiSign.SampleWeb/REQUEST_EXAMPLES.md` 和 `samples/ApiSign.SampleWeb/ApiSign.SampleWeb.http`。

## StrictMode 严格模式

`ApiSignOptions.StrictMode` 控制签名参数的提取策略，默认启用（`true`）。

### 严格模式（默认）

签名参数**必须**通过 Header 传递，且 Header 中的值会覆盖 Query/Body 中的同名参数。同时，Query/Body 中的签名参数会被排除在签名字符串之外，确保签名计算仅依赖业务参数。

```csharp
builder.Services.AddApiSignAuthentication(options =>
{
    options.StrictMode = true; // 默认值
});
```

### 非严格模式

关闭后签名参数（`appId`、`nonce`、`timestamp`、`sign`）可以从 Query String、Request Body 或 Header 中任意位置获取，优先级为 Query/Body 优先，Header 补充。适用于客户端签名参数位置不固定的场景。

```csharp
builder.Services.AddApiSignAuthentication(options =>
{
    options.StrictMode = false;
});
```

**两种模式对比**：

| 行为                 | 严格模式（默认） | 非严格模式             |
| -------------------- | ---------------- | ---------------------- |
| 签名参数来源         | 仅 Header        | Query/Body/Header 均可 |
| Header 同名参数覆盖  | 是               | 否                     |
| 签名参数参与签名计算 | 不参与           | 参与                   |
| JSON 解析失败        | 抛出异常         | 静默忽略并记录警告     |

## 自定义失败响应

默认情况下，签名校验失败会返回如下 JSON：

```json
{
  "code": "InvalidSignature",
  "message": "The request signature is invalid."
}
```

如果你希望改成 `ProblemDetails`、纯文本或自定义结构，可以实现 `IApiSignFailureResponseHandler` 并注册到 DI：

```csharp
using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

public sealed class ProblemDetailsApiSignFailureResponseHandler : IApiSignFailureResponseHandler
{
    public async Task HandleAsync(
        HttpContext httpContext,
        SignValidationResult validationResult,
        CancellationToken cancellationToken = default)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "API sign validation failed",
                Detail = validationResult.ErrorMessage,
                Status = httpContext.Response.StatusCode,
                Type = $"https://httpstatuses.com/{httpContext.Response.StatusCode}",
            },
            cancellationToken);
    }
}

builder.Services.AddSingleton<IApiSignFailureResponseHandler, ProblemDetailsApiSignFailureResponseHandler>();
builder.Services.AddApiSignAuthentication();
```

注册自定义实现后，中间件模式和 `[SaCheckSign]` 特性模式都会复用同一个失败响应处理器。



## Redis Nonce 示例

核心类库默认使用内存 `Nonce` 存储，不直接依赖 Redis。若需要 Redis 防重放存储，可以像示例项目一样自行实现 `INonceStore`。

`samples/ApiSign.SampleWeb` 已提供：

- `RedisNonceStore`
- `RedisNonceOptions`
- `Microsoft.Extensions.Caching.StackExchangeRedis` 接入示例

启用方式：

```json
{
  "RedisNonce": {
    "Enabled": true,
    "Configuration": "localhost:6379",
    "InstanceName": "apisign:"
  }
}
```

示例注册代码：

```csharp
var redisNonceOptions = builder.Configuration
    .GetSection(RedisNonceOptions.SectionName)
    .Get<RedisNonceOptions>() ?? new RedisNonceOptions();

if (redisNonceOptions.Enabled)
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisNonceOptions.Configuration;
        options.InstanceName = redisNonceOptions.InstanceName;
    });
    builder.Services.AddSingleton<INonceStore, RedisNonceStore>();
}
```
