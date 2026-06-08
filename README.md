# ApiSign

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

## Git Hooks

项目已接入 `Husky.Net` 用于本地代码质量管控：

- 本地工具清单：`dotnet-tools.json`
- Hook 配置目录：`.husky`
- 任务配置：`.husky/task-runner.json`

当前启用的质量门禁：

- `commit-msg`：校验提交信息是否符合 Conventional Commits
- `pre-commit`：对暂存区中的 `.cs`、`.csproj`、`.props`、`.targets` 文件执行 `dotnet format`
- `pre-push`：执行 `dotnet build ApiSign.slnx --no-restore`
- `pre-push`：执行 `dotnet test ApiSign.slnx --no-build`

提交信息格式示例：

```text
feat: add Redis nonce store
fix(api): validate timestamp drift
docs(readme)!: describe breaking changes
```

允许的提交类型：

- `build`
- `chore`
- `ci`
- `docs`
- `feat`
- `fix`
- `perf`
- `refactor`
- `revert`
- `style`
- `test`

常用命令：

```bash
dotnet tool restore
dotnet husky install
dotnet husky run --group pre-commit
dotnet husky run --group pre-push
```

说明：

- `Directory.Build.targets` 已接入自动安装逻辑，团队成员在仓库根目录执行 `restore/build` 后会自动补齐 hooks
- 若需临时跳过 hook，可使用 `git commit --no-verify`
- CI/CD 场景可设置环境变量 `HUSKY=0`

## Testing

测试项目 `tests/ApiSign.AspNetCore.Tests` 现已使用 `xUnit v3`：

- 核心测试包：`xunit.v3`
- IDE/VSTest 适配器：`xunit.runner.visualstudio`
- 测试项目使用可执行测试模型，因此已设置 `OutputType` 为 `Exe`

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
