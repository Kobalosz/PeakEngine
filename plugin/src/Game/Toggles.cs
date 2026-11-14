using System;
using System.Collections.Generic;
using System.Diagnostics;
using PeakEngine.Core;

namespace PeakEngine.Game
{
    /// <summary>
    /// Cheats that stay switched on for yourself.
    ///
    /// A button press is a one-off: do it and be done. A toggle is different, because the game
    /// keeps changing things back (stamina drains, hunger grows). So <see cref="Tick"/> runs
    /// EVERY FRAME on the main thread and re-applies whatever is switched on.
    /// </summary>
    public static class Toggles
    {
        private sealed class Toggle
        {
            public string Name;
            public bool On;
            public bool WasApplied;
            public string Error;
            /// <summary>Apply (true) or undo (false) the cheat on your character.</summary>
            public Action<object, bool> Apply;
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static long _nextStatusClear;

        private static readonly List<Toggle> All = new List<Toggle>
        {
            new Toggle
            {
                // Character.infiniteStam: the game's own "never use stamina" switch.
                Name = "infiniteStamina",
                Apply = (me, on) => Reflect.Set(me, "infiniteStam", on),
            },
            new Toggle
            {
                // CharacterData.isInvincible: the game's own "take no damage" switch.
                Name = "invincible",
                Apply = (me, on) => Reflect.Set(me, "data.isInvincible", on),
            },
            new Toggle
            {
                // No built-in switch for this one, so we wipe our statuses a few times per second.
                Name = "noStatus",
                Apply = (me, on) =>
                {
                    if (!on || Clock.ElapsedMilliseconds < _nextStatusClear) return;
                    _nextStatusClear = Clock.ElapsedMilliseconds + 250;
                    Statuses.ClearMineQuietly();
                },
            },
        };

        public static void Set(string name, bool on)
        {
            var toggle = All.Find(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ApiError(404, $"There is no toggle called '{name}'.");
            toggle.On = on;
            toggle.Error = null;
        }

        /// <summary>The state of every toggle as { name: { on, error } } for the control panel.</summary>
        public static JsonObject Describe()
        {
            var all = new JsonObject();
            foreach (var toggle in All)
                all[toggle.Name] = new JsonObject { ["on"] = toggle.On, ["error"] = toggle.Error };
            return all;
        }

        /// <summary>Called every frame on the main thread.</summary>
        public static void Tick()
        {
            object me;
            try { me = Players.Local; }
            catch { return; }
            if (me == null) return;

            foreach (var toggle in All)
            {
                // Nothing to do if it is off and we already undid it.
                if (!toggle.On && !toggle.WasApplied) continue;
                try
                {
                    toggle.Apply(me, toggle.On);
                    toggle.WasApplied = toggle.On;
                }
                catch (Exception e)
                {
                    // Switch a broken toggle off instead of failing 60 times per second.
                    toggle.On = false;
                    toggle.WasApplied = false;
                    toggle.Error = e.Message;
                }
            }
        }
    }
}
