namespace HtfTrainer
{
    /// <summary>
    /// Chat and the game's built-in developer console.
    ///
    /// Server.SendChatMessage is a ServerRpc with RequireOwnership off, so a plain client can post a
    /// message everyone sees. DazedCommands.IsServerCommand is the game's own command parser — it
    /// needs ClientSettings.CheatsEnabled (public static) and the host, and covers spawning items,
    /// skins, islands, achievements and more.
    /// </summary>
    internal static class Commands
    {
        internal static string Last { get; private set; } = "";

        internal static bool CanRunCommands =>
            Server.Instance != null && Server.Instance.IsServerInitialized;

        internal static void SendChat(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (Server.Instance == null) { Last = "not connected"; return; }

            Server.Instance.SendChatMessage(message);
            Last = "sent: " + message;
        }

        /// <summary>Runs a game command such as "/spawn tuna". Enables the cheat flag first.</summary>
        internal static void Run(string command)
        {
            if (string.IsNullOrEmpty(command)) return;

            if (!CanRunCommands) { Last = "commands are host only"; return; }

            ClientSettings.ToggleCheats(to: true);
            var full = command.StartsWith("/") ? command : "/" + command;

            try
            {
                DazedCommands.IsServerCommand(full);
                Last = "ran: " + full;
            }
            catch (System.Exception e)
            {
                Last = "failed: " + e.Message;
            }
        }
    }
}
