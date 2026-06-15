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
                DrawWinActions(rule);

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
            configuration.WinRules.Add(new WinRule());
            configuration.Save();
        }
    }

    private void DrawWinActions(WinRule rule)
    {
        var highlight = rule.HighlightWinningRoll;
        if (ImGui.Checkbox("Highlight winning roll", ref highlight))
        {
            rule.HighlightWinningRoll = highlight;
            configuration.Save();
        }

        var sendEcho = rule.SendEcho;
        if (ImGui.Checkbox("Send bartender echo", ref sendEcho))
        {
            rule.SendEcho = sendEcho;
            configuration.Save();
        }

        if (rule.SendEcho)
        {
            ImGui.SetNextItemWidth(-1);
            var echoMessage = rule.EchoMessage;
            if (ImGui.InputText("Echo message", ref echoMessage, 450))
            {
                rule.EchoMessage = echoMessage;
                configuration.Save();
            }
        }

        ImGui.SetNextItemWidth(-1);
        var chatMessage = rule.ChatMessage;
        if (ImGui.InputText("Win message", ref chatMessage, 450))
        {
            rule.ChatMessage = chatMessage;
            configuration.Save();
        }

        ImGui.TextDisabled("Send win message to:");
        rule.ChatChannels ??= [];
        var channelsChanged = false;
        if (ImGui.BeginTable("WinChatChannels", 3, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var channel in Enum.GetValues<WinChatChannel>())
            {
                ImGui.TableNextColumn();
                var selected = rule.ChatChannels.Contains(channel);
                if (!ImGui.Checkbox(WinNotificationFormatter.GetChannelLabel(channel), ref selected))
                    continue;

                channelsChanged = true;
                if (selected)
                    rule.ChatChannels.Add(channel);
                else
                    rule.ChatChannels.Remove(channel);
            }

            ImGui.EndTable();
        }

        if (channelsChanged)
            configuration.Save();

        ImGui.TextDisabled(
            "Placeholders: {player}, {roll}, {rule}, {payout}, {prize}, {award}. " +
            "No selected channels means no public win message.");
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
