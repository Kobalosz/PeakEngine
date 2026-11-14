using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PeakEngine.Core
{
    /// <summary>
    /// An error that is the caller's fault or simply "not possible right now" (unknown player,
    /// not the host, not in a game...). The message is shown to the user in the control panel.
    /// </summary>
    public class ApiError : Exception
    {
        public readonly int Status;
        public ApiError(int status, string message) : base(message) { Status = status; }
    }

    /// <summary>
    /// Decides what to do with each web request. If you have used Express in Node, this is the
    /// same idea as <c>app.get("/api/state", handler)</c> plus <c>express.static("panel")</c>.
    ///
    ///   /api/...      -> runs a registered handler ON THE GAME'S MAIN THREAD and returns JSON
    ///   anything else -> serves a file from the panel folder (index.html, app.js, ...)
    /// </summary>
    public sealed class Router
    {
        private readonly Dictionary<string, Func<HttpRequest, object>> _routes =
            new Dictionary<string, Func<HttpRequest, object>>(StringComparer.OrdinalIgnoreCase);

        private readonly string _panelFolder;
        private readonly int _port;
        private readonly Func<Func<object>, object> _runOnMainThread;
        private readonly Action<string> _log;

        public Router(string panelFolder, int port, Func<Func<object>, object> runOnMainThread, Action<string> log)
        {
            _panelFolder = Path.GetFullPath(panelFolder);
            _port = port;
            _runOnMainThread = runOnMainThread;
            _log = log ?? (_ => { });
        }

        /// <summary>Register a handler for reading data. GET requests must never change anything.</summary>
        public void Get(string path, Func<HttpRequest, object> handler) => _routes["GET " + path] = handler;

        /// <summary>Register a handler that changes something in the game.</summary>
        public void Post(string path, Func<HttpRequest, object> handler) => _routes["POST " + path] = handler;

        public HttpResponse Handle(HttpRequest request)
        {
            // SECURITY 1: only answer requests that were really addressed to this machine.
            // This stops "DNS rebinding", where a malicious website points its own domain at 127.0.0.1.
            string host = request.Header("Host");
            if (host != "127.0.0.1:" + _port && host != "localhost:" + _port)
                return Error(403, "This server only answers requests for 127.0.0.1:" + _port);

            return request.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                ? HandleApi(request)
                : HandleFile(request);
        }

        // ---- API ---------------------------------------------------------------------------------

        private HttpResponse HandleApi(HttpRequest request)
        {
            // SECURITY 2: any website you visit can make your browser send requests to 127.0.0.1.
            // Two checks make sure only OUR panel can trigger cheats:
            //   - if the browser says which site the request came from (Origin), it must be us;
            //   - changes need a custom header. Browsers refuse to let other sites add custom
            //     headers unless the server allows it (CORS), and we never allow it.
            string origin = request.Header("Origin");
            if (origin != "" && origin != "http://127.0.0.1:" + _port && origin != "http://localhost:" + _port)
                return Error(403, "Requests from other websites are not allowed.");
            if (request.Method != "GET" && request.Header("X-PeakEngine") != "1")
                return Error(403, "Missing the X-PeakEngine header.");

            if (!_routes.TryGetValue(request.Method + " " + request.Path, out var handler))
            {
                bool otherMethod = _routes.ContainsKey((request.Method == "GET" ? "POST " : "GET ") + request.Path);
                return otherMethod
                    ? Error(405, $"{request.Path} does not accept {request.Method}.")
                    : Error(404, $"There is no API route called {request.Path}.");
            }

            try
            {
                object result = _runOnMainThread(() => handler(request));
                return HttpResponse.JsonText(200, Json.Write(result));
            }
            catch (ApiError e)
            {
                return Error(e.Status, e.Message);
            }
            catch (TimeoutException e)
            {
                return Error(503, e.Message);
            }
            catch (MissingGameMemberException e)
            {
                _log($"{request.Path}: {e.Message}");
                return Error(500, e.Message + " Open the Diagnostics tab to see what else is affected.");
            }
            catch (Exception e)
            {
                _log($"{request.Path} failed: {e}");
                return Error(500, $"{e.GetType().Name}: {e.Message}");
            }
        }

        private static HttpResponse Error(int status, string message) =>
            HttpResponse.JsonText(status, Json.Write(new JsonObject { ["error"] = message }));

        // ---- Static files (the control panel itself) ---------------------------------------------

        private static readonly Dictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html; charset=utf-8",
            [".js"] = "text/javascript; charset=utf-8",
            [".mjs"] = "text/javascript; charset=utf-8",
            [".css"] = "text/css; charset=utf-8",
            [".json"] = "application/json; charset=utf-8",
            [".svg"] = "image/svg+xml",
            [".png"] = "image/png",
            [".ico"] = "image/x-icon",
        };

        private HttpResponse HandleFile(HttpRequest request)
        {
            if (request.Method != "GET") return Error(405, "Files can only be read.");

            string relative = request.Path == "/" ? "index.html" : request.Path.TrimStart('/');
            string full;
            try { full = Path.GetFullPath(Path.Combine(_panelFolder, relative)); }
            catch { return HttpResponse.Text(400, "Bad path"); }

            // SECURITY 3: never serve files outside the panel folder (blocks paths like /../../secret.txt).
            if (!full.StartsWith(_panelFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                return HttpResponse.Text(403, "Forbidden");

            if (!File.Exists(full) || !ContentTypes.TryGetValue(Path.GetExtension(full), out var contentType))
            {
                return relative == "index.html"
                    ? HttpResponse.Text(404, "PeakEngine is running, but the control panel files are missing.\nExpected them in: " + _panelFolder)
                    : HttpResponse.Text(404, "Not found");
            }

            return HttpResponse.Bytes(contentType, File.ReadAllBytes(full));
        }
    }

    /// <summary>
    /// Helpers for reading the ?name=value inputs of a request with clear errors when one is
    /// missing or malformed. Use them as <c>request.Int("player")</c>.
    /// </summary>
    public static class RequestInputs
    {
        public static string Text(this HttpRequest request, string name)
        {
            if (!request.Query.TryGetValue(name, out var value) || value.Length == 0)
                throw new ApiError(400, $"Missing the '{name}' parameter.");
            return value;
        }

        public static string TextOr(this HttpRequest request, string name, string fallback) =>
            request.Query.TryGetValue(name, out var value) && value.Length > 0 ? value : fallback;

        public static int Int(this HttpRequest request, string name)
        {
            if (!int.TryParse(request.Text(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                throw new ApiError(400, $"'{name}' must be a whole number.");
            return number;
        }

        public static int IntOr(this HttpRequest request, string name, int fallback) =>
            request.Query.ContainsKey(name) ? request.Int(name) : fallback;

        public static float Float(this HttpRequest request, string name)
        {
            if (!float.TryParse(request.Text(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                || float.IsNaN(number) || float.IsInfinity(number))
                throw new ApiError(400, $"'{name}' must be a number.");
            return number;
        }

        public static bool Bool(this HttpRequest request, string name)
        {
            string value = request.Text(name).ToLowerInvariant();
            if (value == "true" || value == "1") return true;
            if (value == "false" || value == "0") return false;
            throw new ApiError(400, $"'{name}' must be true or false.");
        }
    }
}
