using System.Collections.Generic;
using PeakEngine.Core;
using PeakEngine.Game;

namespace PeakEngine
{
    /// <summary>
    /// THE LIST OF EVERYTHING THE CONTROL PANEL CAN ASK FOR.
    ///
    /// Each line connects a URL to a bit of game code. The JavaScript panel calls these URLs with
    /// fetch(); see panel/js/api.js for the other side.
    ///
    ///   router.Get(...)  -> only reads information
    ///   router.Post(...) -> changes something in the game
    ///
    /// Every handler runs on the game's main thread, so it is safe to touch game objects here.
    /// To add your own cheat, follow docs/04-add-your-own-cheat.md.
    /// </summary>
    public static class Routes
    {
        public const string PluginVersion = "0.1.0";

        public static void Register(Router router)
        {
            // ---- Reading -------------------------------------------------------------------------

            // The panel polls this once a second to draw everything.
            router.Get("/api/state", request => State());

            // The list of all items. Asked for once, because it never changes during a session.
            router.Get("/api/items", request => Items.List());

            // The "does this game version still match?" self-check.
            router.Get("/api/diagnostics", request => Diagnostics.Run());

            // ---- Statuses ------------------------------------------------------------------------

            // POST /api/status/set?player=1001&type=Hunger&value=0.5
            router.Post("/api/status/set", request =>
            {
                object player = Players.Find(request.Int("player"));
                Statuses.Set(player, request.Text("type"), request.Float("value"));
                return Done();
            });

            // POST /api/status/clear?player=1001
            router.Post("/api/status/clear", request =>
            {
                Statuses.ClearAll(Players.Find(request.Int("player")));
                return Done();
            });

            // ---- Players -------------------------------------------------------------------------

            // POST /api/player/stamina?player=1001
            router.Post("/api/player/stamina", request =>
            {
                Players.FillStamina(Players.Find(request.Int("player")));
                return Done();
            });

            // POST /api/player/revive?player=1001
            router.Post("/api/player/revive", request =>
            {
                Players.Revive(Players.Find(request.Int("player")));
                return Done();
            });

            // POST /api/player/teleport?player=1001&to=2001   (moves "player" to where "to" is)
            router.Post("/api/player/teleport", request =>
            {
                Players.Teleport(Players.Find(request.Int("player")), Players.Find(request.Int("to")));
                return Done();
            });

            // ---- Items ---------------------------------------------------------------------------

            // POST /api/items/spawn?item=Coconut&player=1001&mode=drop&count=3
            router.Post("/api/items/spawn", request =>
            {
                Items.Spawn(
                    request.Text("item"),
                    Players.Find(request.Int("player")),
                    request.TextOr("mode", "hand"),
                    request.IntOr("count", 1));
                return Done();
            });

            // ---- Toggles (always-on cheats for yourself) -----------------------------------------

            // POST /api/toggle?name=infiniteStamina&on=true
            router.Post("/api/toggle", request =>
            {
                Toggles.Set(request.Text("name"), request.Bool("on"));
                return Done();
            });
        }

        /// <summary>Everything the panel needs to draw one frame of its UI.</summary>
        private static object State()
        {
            var players = new List<JsonObject>();
            foreach (object character in Players.All())
                players.Add(Players.Describe(character));

            return new JsonObject
            {
                ["version"] = PluginVersion,
                ["inGame"] = Players.Local != null,
                ["inRoom"] = Net.InRoom,
                ["isHost"] = Net.IsHost,
                ["statusTypes"] = Statuses.Names,
                ["computedStatuses"] = Statuses.Computed,
                ["players"] = players,
                ["toggles"] = Toggles.Describe(),
            };
        }

        private static object Done() => new JsonObject { ["ok"] = true };
    }
}
