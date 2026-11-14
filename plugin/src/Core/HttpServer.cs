using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PeakEngine.Core
{
    /// <summary>One incoming web request, already split into its parts.</summary>
    public sealed class HttpRequest
    {
        /// <summary>"GET" or "POST".</summary>
        public string Method = "";
        /// <summary>The part before the "?", e.g. "/api/status/set".</summary>
        public string Path = "";
        /// <summary>The part after the "?", e.g. ?player=3&amp;value=0.5 becomes { player: "3", value: "0.5" }.</summary>
        public Dictionary<string, string> Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Header(string name) => Headers.TryGetValue(name, out var value) ? value : "";
    }

    /// <summary>What we send back to the browser.</summary>
    public sealed class HttpResponse
    {
        public int Status = 200;
        public string ContentType = "text/plain; charset=utf-8";
        public byte[] Body = new byte[0];

        public static HttpResponse Text(int status, string text) =>
            new HttpResponse { Status = status, Body = Encoding.UTF8.GetBytes(text) };

        public static HttpResponse JsonText(int status, string json) =>
            new HttpResponse { Status = status, ContentType = "application/json; charset=utf-8", Body = Encoding.UTF8.GetBytes(json) };

        public static HttpResponse Bytes(string contentType, byte[] body) =>
            new HttpResponse { Status = 200, ContentType = contentType, Body = body };
    }

    /// <summary>
    /// A minimal web server, the C# cousin of Node's <c>http.createServer((req, res) => ...)</c>.
    ///
    /// It listens on a TCP port, reads one HTTP request per connection, asks <c>handler</c> for a
    /// response, sends it and closes the connection. That is all a local control panel needs.
    ///
    /// It is written on top of raw sockets on purpose: sockets are guaranteed to exist inside the
    /// game (its multiplayer needs them), while fancier built-in web server classes might not.
    /// </summary>
    public sealed class HttpServer
    {
        private const int MaxHeaderBytes = 16 * 1024;
        private const int MaxBodyBytes = 64 * 1024;

        private readonly IPAddress _address;
        private readonly int _port;
        private readonly Func<HttpRequest, HttpResponse> _handler;
        private readonly Action<string> _log;
        private Socket _listener;
        private volatile bool _running;

        public HttpServer(IPAddress address, int port, Func<HttpRequest, HttpResponse> handler, Action<string> log)
        {
            _address = address;
            _port = port;
            _handler = handler;
            _log = log ?? (_ => { });
        }

        public void Start()
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Bind(new IPEndPoint(_address, _port));
            _listener.Listen(16);
            _running = true;

            // A background thread does not keep the game alive when you quit.
            new Thread(AcceptLoop) { IsBackground = true, Name = "PeakEngine web server" }.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Close(); } catch { /* already closed */ }
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                Socket client;
                try { client = _listener.Accept(); }
                catch
                {
                    if (!_running) return; // Stop() closed the socket; that is the normal way out.
                    Thread.Sleep(50);
                    continue;
                }
                // Handle each browser connection on a pool thread so a slow one cannot block the others.
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        }

        private void HandleClient(Socket client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                HttpResponse response;
                try
                {
                    var request = ReadRequest(client);
                    response = request == null
                        ? HttpResponse.Text(400, "Bad request")
                        : _handler(request);
                }
                catch (SocketException) { return; } // the browser went away
                catch (Exception e)
                {
                    _log("Request failed: " + e);
                    response = HttpResponse.Text(500, "Internal error");
                }

                Send(client, response);
            }
            catch { /* nothing useful to do if sending fails */ }
            finally
            {
                try { client.Shutdown(SocketShutdown.Both); } catch { }
                try { client.Close(); } catch { }
            }
        }

        // ---- Reading -----------------------------------------------------------------------------

        private static HttpRequest ReadRequest(Socket client)
        {
            // An HTTP request is text: a request line, header lines, a blank line, then an optional body.
            //
            //   POST /api/status/set?player=3&value=0.5 HTTP/1.1
            //   Host: 127.0.0.1:8777
            //   X-PeakEngine: 1
            //   (blank line)
            var buffer = new byte[4096];
            var received = new List<byte>();
            int headerEnd = -1;

            while (headerEnd < 0)
            {
                int count = client.Receive(buffer);
                if (count <= 0) return null;
                for (int i = 0; i < count; i++) received.Add(buffer[i]);
                headerEnd = FindHeaderEnd(received);
                if (headerEnd < 0 && received.Count > MaxHeaderBytes) return null;
            }

            string head = Encoding.ASCII.GetString(received.ToArray(), 0, headerEnd);
            string[] lines = head.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2) return null;

            var request = new HttpRequest { Method = requestLine[0].ToUpperInvariant() };

            string target = requestLine[1];
            int question = target.IndexOf('?');
            request.Path = Uri.UnescapeDataString(question < 0 ? target : target.Substring(0, question));
            if (question >= 0) ParseQuery(target.Substring(question + 1), request.Query);

            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon > 0) request.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            // We do not use request bodies, but we must still read one if the browser sends it,
            // otherwise the connection is closed while the browser is still talking.
            if (int.TryParse(request.Header("Content-Length"), out int bodyLength) && bodyLength > 0)
            {
                if (bodyLength > MaxBodyBytes) return null;
                int alreadyRead = received.Count - (headerEnd + 4);
                while (alreadyRead < bodyLength)
                {
                    int count = client.Receive(buffer);
                    if (count <= 0) break;
                    alreadyRead += count;
                }
            }

            return request;
        }

        /// <summary>Position of the blank line (\r\n\r\n) that ends the headers, or -1.</summary>
        private static int FindHeaderEnd(List<byte> bytes)
        {
            for (int i = 0; i + 3 < bytes.Count; i++)
                if (bytes[i] == '\r' && bytes[i + 1] == '\n' && bytes[i + 2] == '\r' && bytes[i + 3] == '\n')
                    return i;
            return -1;
        }

        private static void ParseQuery(string query, Dictionary<string, string> into)
        {
            foreach (var pair in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');
                string key = equals < 0 ? pair : pair.Substring(0, equals);
                string value = equals < 0 ? "" : pair.Substring(equals + 1);
                // "+" means space in query strings; %20-style escapes are handled by UnescapeDataString.
                into[Uri.UnescapeDataString(key.Replace('+', ' '))] = Uri.UnescapeDataString(value.Replace('+', ' '));
            }
        }

        // ---- Writing -----------------------------------------------------------------------------

        private static void Send(Socket client, HttpResponse response)
        {
            var head = new StringBuilder();
            head.Append("HTTP/1.1 ").Append(response.Status).Append(' ').Append(Reason(response.Status)).Append("\r\n");
            head.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
            head.Append("Content-Length: ").Append(response.Body.Length).Append("\r\n");
            head.Append("Cache-Control: no-store\r\n");
            head.Append("X-Content-Type-Options: nosniff\r\n");
            head.Append("Connection: close\r\n\r\n");

            SendAll(client, Encoding.ASCII.GetBytes(head.ToString()));
            SendAll(client, response.Body);
        }

        private static void SendAll(Socket client, byte[] data)
        {
            int sent = 0;
            while (sent < data.Length)
                sent += client.Send(data, sent, data.Length - sent, SocketFlags.None);
        }

        private static string Reason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 500: return "Internal Server Error";
                case 503: return "Service Unavailable";
                default: return "Status";
            }
        }
    }
}
