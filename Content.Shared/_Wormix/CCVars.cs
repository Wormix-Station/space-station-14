// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

// Wormix-Changes-start
public sealed partial class CCVars
{
    /// <summary>
    ///     URL of the Discord webhook which will receive ban notifications. If left empty, disables the webhook.
    /// </summary>
    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("discord.ban_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     URL of the Discord webhook which will receive admin fax notifications. If left empty, disables the webhook.
    /// </summary>
    public static readonly CVarDef<string> DiscordFaxWebhook =
        CVarDef.Create("discord.fax_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     URL of the Discord webhook which will receive vote results. If left empty, disables the webhook.
    /// </summary>
    public static readonly CVarDef<string> DiscordVoteResultsWebhook =
        CVarDef.Create("discord.vote_webhook_alt", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
// Wormix-Changes-end
