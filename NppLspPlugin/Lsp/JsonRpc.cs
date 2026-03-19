using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NppLspPlugin.Lsp
{
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = "";

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class JsonRpcNotification
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("method")]
        public string Method { get; set; } = "";

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }
    }

    public class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
    }

    public static class JsonRpc
    {
        public static byte[] SerializeRequest(int id, string method, object? @params)
        {
            var paramsElement = @params != null
                ? JsonSerializer.SerializeToElement(@params, @params.GetType(), LspJsonContext.Default)
                : (JsonElement?)null;

            var request = new JsonRpcRequest
            {
                Id = id,
                Method = method,
                Params = paramsElement
            };

            var json = JsonSerializer.Serialize(request, LspJsonContext.Default.JsonRpcRequest);
            return WrapWithContentLength(json);
        }

        public static byte[] SerializeNotification(string method, object? @params)
        {
            var paramsElement = @params != null
                ? JsonSerializer.SerializeToElement(@params, @params.GetType(), LspJsonContext.Default)
                : (JsonElement?)null;

            var notification = new JsonRpcNotification
            {
                Method = method,
                Params = paramsElement
            };

            var json = JsonSerializer.Serialize(notification, LspJsonContext.Default.JsonRpcNotification);
            return WrapWithContentLength(json);
        }

        public static JsonRpcResponse? Deserialize(string json)
        {
            return JsonSerializer.Deserialize(json, LspJsonContext.Default.JsonRpcResponse);
        }

        private static byte[] WrapWithContentLength(string json)
        {
            var contentBytes = Encoding.UTF8.GetBytes(json);
            var header = $"Content-Length: {contentBytes.Length}\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);

            var result = new byte[headerBytes.Length + contentBytes.Length];
            Array.Copy(headerBytes, 0, result, 0, headerBytes.Length);
            Array.Copy(contentBytes, 0, result, headerBytes.Length, contentBytes.Length);
            return result;
        }
    }
}
