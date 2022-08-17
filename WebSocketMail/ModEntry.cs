using System;
using System.Collections.Generic;
using System.Text.Json;
using MailFrameworkMod;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WebSocketMail
{
    public class ModEntry : Mod
    {
        const string MessageType = "letter";

        ModConfig config;
        readonly Queue<Letter> QueuedMessages = new Queue<Letter>();
        static readonly List<string> RequiredMods = new List<string> {
            "DIGUS.MailFrameworkMod",
        };
        WebsocketMessageClient messageClient;

        public override void Entry(IModHelper helper)
        {
            config = Helper.ReadConfig<ModConfig>();
            if (string.IsNullOrWhiteSpace (config.wsHost)) {
                throw new Exception($"Invalid configuration. Missing {nameof (ModConfig.wsHost)} property.");
			}

            messageClient = new WebsocketMessageClient();
			messageClient.OnMessage += MessageClient_OnMessage;

            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.DayEnding += GameLoop_DayEnding;
        }

		private void MessageClient_OnMessage(object sender, string e)
        {
            Monitor.Log($"Websocket message received: {e}");
            QueueMessageIfApplicable(e);
        }

		private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var allModsLoaded = true;
            RequiredMods.ForEach(x => {
                if (!Helper.ModRegistry.IsLoaded(x)) {
                    Monitor.Log($"Required mod {x} is not loaded.", LogLevel.Error);
                    allModsLoaded = false;
                }
            });
            if (!allModsLoaded) {
                return;
            }
            Monitor.Log($"Mods loaded. Starting to connect and listen...");
            messageClient.ConnectAndListen(new Uri(config.wsHost), TimeSpan.FromSeconds(5));
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            PushQueuedMessages();
        }

		private void GameLoop_DayEnding(object sender, DayEndingEventArgs e)
		{
            PushQueuedMessages();
        }

        void QueueMessageIfApplicable (string message) {
            if (string.IsNullOrWhiteSpace (message)) {
                return;
			}

            WebsocketMessage parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<WebsocketMessage>(message);
            }
            catch { }

            if (parsed == null) {
                Monitor.Log($"Message could not be parsed into {nameof(WebsocketMessage)}");
                return;
            }

            if (parsed.type != MessageType)
            {
                Monitor.Log($"Ignoring websocket message with type: {parsed.type}");
                return;
            }

            Monitor.Log($"Writing letter...");
            var letter = BuildLetter(parsed.data, parsed.user);
            QueuedMessages.Enqueue(letter);
            Monitor.Log($"Letter mailed!");
        }

        void PushQueuedMessages () {
            if (!Context.IsWorldReady) {
                return;
            }
            while (QueuedMessages.Count > 0) {
                SaveLetter(QueuedMessages.Dequeue());
            }
		}

        static Letter BuildLetter (string message, string author)
        {
            var id = Guid.NewGuid().ToString ();
            var letterMessage = $"{message}^^Love, {author}";
            return new Letter(
                id,
                letterMessage,
                (l) => !Game1.player.mailReceived.Contains(l.Id),
                (l) => Game1.player.mailReceived.Add(l.Id)
            );
        }

        private void SaveLetter (Letter letter)
		{
            MailDao.SaveLetter(letter);
		}
    }
}
