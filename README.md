# WebSocketMail
A simple Stardew Valley mod for receiving custom letters via a websocket connection. The mod connects to a websocket server defined in `config.json`, and begins listening for messages. As long as the message is in the expected JSON format, the mod will interpret it as a letter to add to the game using [Digus' Mail Framework Mod](https://www.nexusmods.com/stardewvalley/mods/1536).

## Dependencies
- [SMAPI](https://www.nexusmods.com/stardewvalley/mods/2400)
- [Mail Framework Mod](https://www.nexusmods.com/stardewvalley/mods/1536)

## Configuration
Configuring the mod involves editing the included `config.json` file after installing it in your Stardew Valley mods folder:
```
{
	"wsHost": ""
}
```
The `wsHost` property should be changed to be the full URL of your websocket server, including the protocol. Some examples may be:
- `ws://localhost:8080`
- `ws://192.168.1.123:9000`
- `wss://my-host.com/websocket`

You can connect to whatever websocket you like as long as you have the ability to receive websocket messages formatted in the expected format.

## Message format
The class `WebsocketMessage` defines the expected format of the JSON messages read by the websocket connection:
```
public class WebsocketMessage
{
	public string type { get; set; }
	public string user { get; set; }
	public string data { get; set; }
}
```
When a message is received by the websocket client, it attempts to deserialize it into a `WebsocketMessage`. If it fails, then the message is ignored.

After being deserialized, the `type` value is checked. This mod expects the `type` to be the value `"letter"` in order for it to be considered a message that should ultimately be added to the game for the player to receive in their mailbox. Messages with a different `type` value will be ignored.

The `user` property contains the "author" of the letter which will be displayed at the bottom, and the `data` property contains the bulk of the letter itself.

## Message queuing

As soon as the game launches, the mod connects to the websocket server and starts listening for new messages. However, these messages are not "delivered" to the player via the Mail Framework Mod until the save has been loaded.

Messages that are received and parsed from the websocket server are queued up in-memory and stay there until either the player loads a save file, or until right when a day is ending. This is because the Mail Framework Mod seems to be picky with when saved messages will actually show up for the player. So in general, with this mod, messages sent via the websocket to the mod will not show up for the player until the following day in the game.
