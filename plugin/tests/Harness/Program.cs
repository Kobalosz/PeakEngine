// Starts the real PeakEngine web server and routes, but pointed at the pretend game in ../FakeGame.
//
//   dotnet run --project plugin/tests/Harness -- [port] [panel folder] [host|client]
//
// The loop at the bottom plays the role of Unity: it calls the per-frame functions 60 times a second.
using System;
using System.IO;
using System.Net;
using System.Threading;
using PeakEngine;
using PeakEngine.Core;
using PeakEngine.Game;

int port = args.Length > 0 ? int.Parse(args[0]) : 8777;
string panelFolder = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "panel");
bool host = args.Length <= 2 || args[2] != "client";

// Make sure the pretend game's DLL is loaded so PeakEngine can find its classes by name.
FakeWorld.Create(host);

var router = new Router(panelFolder, port, work => MainThread.Run(work), Console.Error.WriteLine);
Routes.Register(router);
// Test-only route: lets the tests see what the pretend game recorded.
router.Get("/api/test/world", _ => new JsonObject
{
    ["spawned"] = Photon.Pun.PhotonNetwork.Spawned,
    ["rpcs"] = Photon.Pun.PhotonNetwork.RpcLog,
    ["myHand"] = Character.localCharacter.refs.items.inHand,
    ["friendHand"] = Character.AllCharacters[1].refs.items.inHand,
    ["invincible"] = Character.localCharacter.data.IsInvincibleForTests,
    ["infiniteStam"] = Character.localCharacter.infiniteStam,
    ["pushes"] = Character.localCharacter.refs.afflictions.pushes,
    ["friendPranked"] = Character.AllCharacters[1].pranked,
    ["friendBlocked"] = Character.AllCharacters[1].blocked,
});

new HttpServer(IPAddress.Loopback, port, router.Handle, Console.Error.WriteLine).Start();
Console.WriteLine($"Pretend PEAK running ({(host ? "you are host" : "you are NOT host")}). Panel: http://127.0.0.1:{port}");

while (true)
{
    FakeWorld.Simulate(1f / 60f);
    MainThread.Pump();
    Toggles.Tick();
    Thread.Sleep(16);
}
