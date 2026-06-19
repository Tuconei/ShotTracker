using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ShotTracker.Models;
using ShotTracker.Services;

namespace ShotTracker.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly Dictionary<Guid, string> winningRangeInputs = [];
    private WinActionProfile? copiedWinActionProfile;
    private string newVenueProfileName = "New venue";

    public ConfigWindow(Plugin plugin)
        : base("ShotTracker Settings###ShotTrackerConfig")
    {
        configuration = plugin.Configuration;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 430),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        DrawVenueProfiles();
        ImGui.Separator();

        ImGui.Text("Pricing and nightly split");
        ImGui.SetNextItemWidth(180);
        var shotPrice = configuration.ShotPrice;
        if (ImGui.InputInt("Gil per shot", ref shotPrice, 10_000, 100_000))
        {
            configuration.ShotPrice = Math.Max(1, shotPrice);
            configuration.Save();
        }

        var jackpotPercent = configuration.JackpotPercent;
        if (DrawPercentInput("Jackpot %", ref jackpotPercent))
        {
            configuration.JackpotPercent = jackpotPercent;
            configuration.Save();
        }

        var housePercent = configuration.HousePercent;
        if (DrawPercentInput("House %", ref housePercent))
        {
            configuration.HousePercent = housePercent;
            configuration.Save();
        }

        var dealerPercent = configuration.DealerPercent;
        if (DrawPercentInput("Dealer %", ref dealerPercent))
        {
            configuration.DealerPercent = dealerPercent;
            configuration.Save();
        }

        var total = configuration.JackpotPercent + configuration.HousePercent + configuration.DealerPercent;
        var totalColor = total <= 100
            ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
            : new Vector4(1f, 0.35f, 0.35f, 1f);
        ImGui.TextColored(totalColor, $"Allocated: {total:0.##}% | Reserve: {Math.Max(0, 100 - total):0.##}%");

        if (configuration.ActiveSession == null)
        {
            var jackpot = (int)Math.Clamp(configuration.JackpotBalance, 0, 999_999_999);
            ImGui.SetNextItemWidth(180);
            if (ImGui.InputInt("Current jackpot", ref jackpot, 10_000, 100_000))
            {
                configuration.JackpotBalance = Math.Max(0, jackpot);
                configuration.Save();
            }
        }
        else
        {
            ImGui.Text($"Current jackpot: {configuration.JackpotBalance:N0} gil");
            ImGui.TextDisabled("Close the active night before adjusting the jackpot manually.");
        }

        ImGui.TextDisabled("Payouts are deducted from this jackpot. Fixed and percentage payouts are capped at its balance.");
        ImGui.Separator();
        ImGui.Text("Winning numbers and ranges");

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Paid rolls exhausted message"))
        {
            configuration.PaidRollsExhaustedMessageProfile ??= new PaidRollsExhaustedMessageProfile();
            DrawPaidRollsExhaustedMessage(configuration.PaidRollsExhaustedMessageProfile);
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Default win actions"))
        {
            configuration.DefaultWinActionProfile ??= new WinActionProfile();

            if (ImGui.Button("Apply default actions to all rules"))
            {
                foreach (var rule in configuration.WinRules)
                    rule.ApplyActionProfile(configuration.DefaultWinActionProfile);

                configuration.Save();
            }

            DrawWinActions(configuration.DefaultWinActionProfile);
        }

        var removeIndex = -1;
        for (var i = 0; i < configuration.WinRules.Count; i++)
        {
            var rule = configuration.WinRules[i];
            ImGui.PushID(rule.Id.ToString());
            ImGui.Separator();
            ImGui.Text($"Rule {i + 1}");

            var enabled = rule.Enabled;
            if (ImGui.Checkbox("Enabled", ref enabled))
            {
                rule.Enabled = enabled;
                configuration.Save();
            }

            ImGui.SameLine();
            ImGui.SetNextItemWidth(220);
            var label = rule.Label;
            if (ImGui.InputText("Label", ref label, 64))
            {
                rule.Label = label;
                configuration.Save();
            }

            if (!winningRangeInputs.TryGetValue(rule.Id, out var winningRange))
                winningRange = rule.WinningRangeText;

            ImGui.SetNextItemWidth(120);
            if (ImGui.InputText("Winning range", ref winningRange, 7))
            {
                winningRangeInputs[rule.Id] = winningRange;
                if (rule.TrySetWinningRange(winningRange))
                    configuration.Save();
            }
            else
            {
                winningRangeInputs[rule.Id] = winningRange;
            }

            if (!rule.TrySetWinningRange(winningRange))
                ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), "Use 0-999 or a range such as 0-99.");

            var kind = (int)rule.PayoutKind;
            ImGui.SetNextItemWidth(180);
            if (ImGui.Combo("Payout type", ref kind, "Fixed gil\0Jackpot percentage\0Non-gil prize\0"))
            {
                rule.PayoutKind = (PayoutKind)kind;
                configuration.Save();
            }

            if (rule.PayoutKind == PayoutKind.FixedGil)
            {
                var payoutGil = (int)Math.Clamp(rule.FixedPayoutGil, 0, 999_999_999);
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputInt("Payout gil", ref payoutGil, 10_000, 100_000))
                {
                    rule.FixedPayoutGil = Math.Max(0, payoutGil);
                    configuration.Save();
                }

                var fixedPayoutFromJackpot = rule.FixedPayoutFromJackpot;
                if (ImGui.Checkbox("Deduct fixed payout from jackpot", ref fixedPayoutFromJackpot))
                {
                    rule.FixedPayoutFromJackpot = fixedPayoutFromJackpot;
                    configuration.Save();
                }
            }
            else if (rule.PayoutKind == PayoutKind.JackpotPercentage)
            {
                var payoutPercent = rule.JackpotPayoutPercent;
                ImGui.SetNextItemWidth(140);
                if (ImGui.InputFloat("Jackpot payout %", ref payoutPercent, 1, 10, "%.2f"))
                {
                    rule.JackpotPayoutPercent = Math.Max(0, payoutPercent);
                    configuration.Save();
                }
            }
            else
            {
                ImGui.SetNextItemWidth(300);
                var externalPrize = rule.ExternalPrize;
                if (ImGui.InputText("Prize name", ref externalPrize, 128))
                {
                    rule.ExternalPrize = externalPrize;
                    configuration.Save();
                }
                ImGui.TextDisabled("Tracked in statistics without affecting the jackpot.");
            }

            var reroll = rule.GrantsReroll;
            if (ImGui.Checkbox("Grants a reroll", ref reroll))
            {
                rule.GrantsReroll = reroll;
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Remove"))
                removeIndex = i;

            if (ImGui.CollapsingHeader("Win actions"))
            {
                DrawWinActionCopyControls(rule);
                DrawWinActions(rule);
            }

            ImGui.PopID();
        }

        if (removeIndex >= 0)
        {
            winningRangeInputs.Remove(configuration.WinRules[removeIndex].Id);
            configuration.WinRules.RemoveAt(removeIndex);
            configuration.Save();
        }

        if (ImGui.Button("Add Winning Number"))
        {
            var rule = new WinRule();
            if (configuration.DefaultWinActionProfile != null)
                rule.ApplyActionProfile(configuration.DefaultWinActionProfile);

            configuration.WinRules.Add(rule);
            configuration.Save();
        }
    }

    private void DrawVenueProfiles()
    {
        ImGui.Text("Venue profiles");
        ImGui.TextDisabled("Profiles save pricing, split, jackpot, default win actions, and winning rules.");

        ImGui.SetNextItemWidth(260);
        ImGui.InputText("New profile name", ref newVenueProfileName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Create profile"))
        {
            var profile = configuration.CaptureVenueProfile(newVenueProfileName);
            configuration.VenueProfiles.Add(profile);
            configuration.ActiveVenueProfileId = profile.Id;
            configuration.Save();
        }

        if (configuration.VenueProfiles.Count == 0)
        {
            ImGui.TextDisabled("No saved profiles yet.");
            return;
        }

        var removeIndex = -1;
        for (var i = 0; i < configuration.VenueProfiles.Count; i++)
        {
            var profile = configuration.VenueProfiles[i];
            ImGui.PushID(profile.Id.ToString());

            var active = configuration.ActiveVenueProfileId == profile.Id ? "active" : "saved";
            ImGui.Text($"{profile.Name} ({active})");

            ImGui.SetNextItemWidth(220);
            var name = profile.Name;
            if (ImGui.InputText("Name", ref name, 64))
            {
                profile.Name = name.Trim().Length == 0 ? "New venue" : name.Trim();
                configuration.Save();
            }

            var canLoad = configuration.ActiveSession == null;
            if (!canLoad)
                ImGui.BeginDisabled();

            if (ImGui.Button("Load"))
            {
                configuration.ApplyVenueProfile(profile);
                winningRangeInputs.Clear();
                configuration.Save();
            }

            if (!canLoad)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("Save current"))
            {
                configuration.SaveVenueProfile(profile);
                configuration.Save();
            }

            ImGui.SameLine();
            if (ImGui.Button("Delete"))
                removeIndex = i;

            ImGui.PopID();
        }

        if (configuration.ActiveSession != null)
            ImGui.TextDisabled("Close the active night before loading a different venue profile.");

        if (removeIndex >= 0)
        {
            var removed = configuration.VenueProfiles[removeIndex];
            configuration.VenueProfiles.RemoveAt(removeIndex);
            if (configuration.ActiveVenueProfileId == removed.Id)
                configuration.ActiveVenueProfileId = null;

            configuration.Save();
        }
    }

    private void DrawWinActions(WinRule rule)
    {
        DrawWinActions(rule.ToActionProfile(), rule.ApplyActionProfile);
    }

    private void DrawWinActions(WinActionProfile profile)
    {
        DrawWinActions(profile, updated =>
        {
            profile.HighlightWinningRoll = updated.HighlightWinningRoll;
            profile.SendEcho = updated.SendEcho;
            profile.EchoMessage = updated.EchoMessage;
            profile.ChatMessage = updated.ChatMessage;
            profile.ChatChannels = [.. updated.ChatChannels];
        });
    }

    private void DrawWinActions(WinActionProfile profile, Action<WinActionProfile> apply)
    {
        var updated = new WinActionProfile
        {
            HighlightWinningRoll = profile.HighlightWinningRoll,
            SendEcho = profile.SendEcho,
            EchoMessage = profile.EchoMessage,
            ChatMessage = profile.ChatMessage,
            ChatChannels = [.. profile.ChatChannels],
        };

        var highlight = updated.HighlightWinningRoll;
        if (ImGui.Checkbox("Highlight winning roll", ref highlight))
        {
            updated.HighlightWinningRoll = highlight;
            apply(updated);
            configuration.Save();
        }

        var sendEcho = updated.SendEcho;
        if (ImGui.Checkbox("Send bartender echo", ref sendEcho))
        {
            updated.SendEcho = sendEcho;
            apply(updated);
            configuration.Save();
        }

        if (updated.SendEcho)
        {
            ImGui.SetNextItemWidth(-1);
            var echoMessage = updated.EchoMessage;
            if (ImGui.InputText("Echo message", ref echoMessage, 450))
            {
                updated.EchoMessage = echoMessage;
                apply(updated);
                configuration.Save();
            }
        }

        ImGui.SetNextItemWidth(-1);
        var chatMessage = updated.ChatMessage;
        if (ImGui.InputText("Win message", ref chatMessage, 450))
        {
            updated.ChatMessage = chatMessage;
            apply(updated);
            configuration.Save();
        }

        ImGui.TextDisabled("Send win message to:");
        updated.ChatChannels ??= [];
        var channelsChanged = false;
        if (ImGui.BeginTable("WinChatChannels", 3, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var channel in Enum.GetValues<WinChatChannel>())
            {
                ImGui.TableNextColumn();
                var selected = updated.ChatChannels.Contains(channel);
                if (!ImGui.Checkbox(WinNotificationFormatter.GetChannelLabel(channel), ref selected))
                    continue;

                channelsChanged = true;
                if (selected)
                    updated.ChatChannels.Add(channel);
                else
                    updated.ChatChannels.Remove(channel);
            }

            ImGui.EndTable();
        }

        if (channelsChanged)
        {
            apply(updated);
            configuration.Save();
        }

        ImGui.TextDisabled(
            "Placeholders: {player}, {roll}, {rule}, {payout}, {prize}, {award}. " +
            "No selected channels means no public win message.");
    }

    private void DrawWinActionCopyControls(WinRule rule)
    {
        if (ImGui.Button("Copy actions"))
            copiedWinActionProfile = rule.ToActionProfile();

        ImGui.SameLine();
        if (copiedWinActionProfile == null)
            ImGui.BeginDisabled();

        if (ImGui.Button("Paste actions") && copiedWinActionProfile != null)
        {
            rule.ApplyActionProfile(copiedWinActionProfile);
            configuration.Save();
        }

        if (copiedWinActionProfile == null)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Use default"))
        {
            configuration.DefaultWinActionProfile ??= new WinActionProfile();
            rule.ApplyActionProfile(configuration.DefaultWinActionProfile);
            configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Save as default"))
        {
            configuration.DefaultWinActionProfile = rule.ToActionProfile();
            configuration.Save();
        }
    }

    private void DrawPaidRollsExhaustedMessage(PaidRollsExhaustedMessageProfile profile)
    {
        var sendEcho = profile.SendEcho;
        if (ImGui.Checkbox("Send exhausted-rolls bartender echo", ref sendEcho))
        {
            profile.SendEcho = sendEcho;
            configuration.Save();
        }

        if (profile.SendEcho)
        {
            ImGui.SetNextItemWidth(-1);
            var echoMessage = profile.EchoMessage;
            if (ImGui.InputText("Exhausted-rolls echo message", ref echoMessage, 450))
            {
                profile.EchoMessage = echoMessage;
                configuration.Save();
            }
        }

        ImGui.SetNextItemWidth(-1);
        var chatMessage = profile.ChatMessage;
        if (ImGui.InputText("Exhausted-rolls chat message", ref chatMessage, 450))
        {
            profile.ChatMessage = chatMessage;
            configuration.Save();
        }

        ImGui.TextDisabled("Send exhausted-rolls message to:");
        profile.ChatChannels ??= [];
        var channelsChanged = false;
        if (ImGui.BeginTable("PaidRollsExhaustedChatChannels", 3, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var channel in Enum.GetValues<WinChatChannel>())
            {
                ImGui.TableNextColumn();
                var selected = profile.ChatChannels.Contains(channel);
                if (!ImGui.Checkbox(WinNotificationFormatter.GetChannelLabel(channel), ref selected))
                    continue;

                channelsChanged = true;
                if (selected)
                    profile.ChatChannels.Add(channel);
                else
                    profile.ChatChannels.Remove(channel);
            }

            ImGui.EndTable();
        }

        if (channelsChanged)
            configuration.Save();

        ImGui.TextDisabled(
            "Placeholders: {player}, {rolls}, {paid}. No selected channels means no public exhausted-rolls message.");
    }

    private bool DrawPercentInput(string label, ref float value)
    {
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat(label, ref value, 1, 5, "%.2f"))
        {
            value = Math.Max(0, value);
            return true;
        }

        return false;
    }
}
