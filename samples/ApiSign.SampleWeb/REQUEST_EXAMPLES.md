# ApiSign.SampleWeb 请求示例

本文演示如何对示例接口 `POST /api/payment/transfer` 生成签名并发起请求。

## 示例接口

- 地址：`http://localhost:5186/api/payment/transfer`
- 方法：`POST`
- 示例密钥配置：`demo-app -> secret-001`
- 默认算法：`HMACSHA256`

请求体：

```json
{
  "orderId": "ORD-1001",
  "amount": 99.50,
  "currency": "CNY"
}
```

## 签名参数

示例中使用以下签名参数：

- `appId`: `demo-app`
- `nonce`: `n-20260605-001`
- `timestamp`: `1760000000`

最终参与签名的参数为：

- `amount=99.50`
- `appId=demo-app`
- `currency=CNY`
- `nonce=n-20260605-001`
- `orderId=ORD-1001`
- `timestamp=1760000000`

## Canonical String

按照当前组件规则：

1. 合并签名参数和业务参数
2. 排除 `sign`
3. 按参数名升序排序
4. 对键和值做 URL Encode
5. 使用 `HMACSHA256(secretKey, canonicalString)`

得到的 Canonical String 为：

```text
amount=99.50&appId=demo-app&currency=CNY&nonce=n-20260605-001&orderId=ORD-1001&timestamp=1760000000
```

使用密钥 `secret-001` 计算得到签名：

```text
174B6E0466DB2D49C7D45D31584BCC8BFB83E09A4C0CA86AFD146FC2D31CA1B6
```

## C# 生成签名

```csharp
using System.Security.Cryptography;
using System.Text;

var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
{
    ["amount"] = "99.50",
    ["appId"] = "demo-app",
    ["currency"] = "CNY",
    ["nonce"] = "n-20260605-001",
    ["orderId"] = "ORD-1001",
    ["timestamp"] = "1760000000",
};

var canonical = string.Join(
    "&",
    parameters.Select(pair =>
        $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

var secretKey = "secret-001";
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)));

Console.WriteLine(canonical);
Console.WriteLine(signature);
```

## JavaScript 生成签名

```javascript
const crypto = require("crypto");

const parameters = {
  amount: "99.50",
  appId: "demo-app",
  currency: "CNY",
  nonce: "n-20260605-001",
  orderId: "ORD-1001",
  timestamp: "1760000000"
};

const canonical = Object.keys(parameters)
  .sort()
  .map(key => `${encodeURIComponent(key)}=${encodeURIComponent(parameters[key])}`)
  .join("&");

const signature = crypto
  .createHmac("sha256", "secret-001")
  .update(canonical, "utf8")
  .digest("hex")
  .toUpperCase();

console.log(canonical);
console.log(signature);
```

## curl 请求示例

```bash
curl -X POST "http://localhost:5186/api/payment/transfer?appId=demo-app&nonce=n-20260605-001&timestamp=1760000000&sign=174B6E0466DB2D49C7D45D31584BCC8BFB83E09A4C0CA86AFD146FC2D31CA1B6" \
  -H "Content-Type: application/json" \
  -d "{\"orderId\":\"ORD-1001\",\"amount\":99.50,\"currency\":\"CNY\"}"
```

## PowerShell 请求示例

```powershell
$uri = "http://localhost:5186/api/payment/transfer?appId=demo-app&nonce=n-20260605-001&timestamp=1760000000&sign=174B6E0466DB2D49C7D45D31584BCC8BFB83E09A4C0CA86AFD146FC2D31CA1B6"
$body = @{
    orderId = "ORD-1001"
    amount = 99.50
    currency = "CNY"
} | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri $uri -ContentType "application/json" -Body $body
```

## 签名失败返回示例

示例项目已注册自定义 `IApiSignFailureResponseHandler`，所以签名校验失败时会返回统一结构，而不是默认的 `code/message`：

```json
{
  "success": false,
  "errorCode": "MissingParameters",
  "errorMessage": "Missing required signing parameters.",
  "traceId": "00-5e2cb7df6f4e2f99f58f90b1d6e2f654-8f0df40b1e8ef0d8-00",
  "path": "/api/payment/transfer",
  "timestamp": "2026-06-05T12:00:00+00:00"
}
```

例如漏传 `sign` 时，请求可能类似：

```bash
curl -X POST "http://localhost:5186/api/payment/transfer?appId=demo-app&nonce=n-20260605-001&timestamp=1760000000" \
  -H "Content-Type: application/json" \
  -d "{\"orderId\":\"ORD-1001\",\"amount\":99.50,\"currency\":\"CNY\"}"
```

## 运行时注意事项

- 上面使用的是固定示例 `timestamp` 和 `nonce`，主要用于说明签名过程。
- 真正调用运行中的服务时，需要使用当前时间戳重新计算签名，否则会触发时间戳过期校验。
- 同一个 `nonce` 不能重复使用，否则会触发防重放校验。
- 若你启用了严格模式，需要把 `appId`、`nonce`、`timestamp`、`sign` 放到请求头中，而不是 Query String。
