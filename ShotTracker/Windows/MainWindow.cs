using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ShotTracker.Models;

namespace ShotTracker.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string participantName = string.Empty;
    private int tradeAmount;
    private int manualRoll;
    private string status = "Start a night to begin.";
    private bool statusIsError;

    public MainWindow(Plugin plugin)
        : base("ShotTracker###ShotTrackerMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public void SetStatus(string message, bool isError)
    {
        status = message;
        statusIsError = isError;
    }

    public override void Draw()
    {
        DrawNightControls();
        ImGui.Separator();

        var session = plugin.Sessions.ActiveSession;
        if (session == null)
        {
            ImGui.TextWrapped("No night is active. Your configured jackpot and prior night history are preserved.");
            DrawHistory();
            return;
        }

        DrawSummary(session);
        ImGui.Separator();
        DrawPlayerControls();
        ImGui.Separator();
        DrawRolls(session);
        ImGui.Separator();
        DrawSales(session);
    }

    private void DrawNightControls()
    {
        if (plugin.Sessions.ActiveSession == null)
        {
            if (ImGui.Button("Start Night"))
                Apply(plugin.Sessions.StartNight());
        }
        else
        {
            if (ImGui.Button("Close Night"))
                Apply(plugin.Sessions.CloseNight());
        }

        ImGui.SameLine();
        if (ImGui.Button("Settings"))
            plugin.ToggleConfigUi();

        ImGui.SameLine();
        var color = statusIsError
            ? new Vector4(1f, 0.35f, 0.35f, 1f)
            : new Vector4(0.45f, 0.9f, 0.55f, 1f);
        ImGui.TextColored(color, status);
    }

    private void DrawSummary(NightSession session)
    {
        if (ImGui.BeginTable("NightSummary", 4, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextColumn();
            Metric("Intake", session.TotalIntake);
            ImGui.TableNextColumn();
            Metric("Jackpot", plugin.Configuration.JackpotBalance);
            ImGui.TableNextColumn();
            Metric("House cut", session.HouseCut);
            ImGui.TableNextColumn();
            Metric("Dealer cut", session.DealerCut);
            ImGui.EndTable();
        }

        ImGui.TextDisabled(
            $"Payouts: {session.TotalPayouts:N0} gil | Jackpot added: {session.JackpotContributions:N0} gil | " +
            $"Unallocated reserve: {session.UnallocatedReserve:N0} gil");
    }

    private void DrawPlayerControls()
    {
        var pendingTrade = plugin.Sessions.PendingTrade;
        if (pendingTrade != null)
        {
            ImGui.TextColored(
                new Vector4(1f, 0.78f, 0.25f, 1f),
                $"Waiting for {pendingTrade.PlayerName}: " +
                $"{pendingTrade.ReceivedAmount:N0} / {pendingTrade.ExpectedAmount:N0} gil verified");
            ImGui.TextDisabled(
                $"{pendingTrade.ExpectedAmount - pendingTrade.ReceivedAmount:N0} gil remaining. " +
                "Multiple incoming trades are accumulated; rolls are credited when the full amount is verified.");

            if (pendingTrade.LastObservedAmount is { } observedAmount)
            {
                ImGui.TextColored(
                    new Vector4(1f, 0.45f, 0.35f, 1f),
                    $"Last unmatched trade: {pendingTrade.LastObservedPlayer}, {observedAmount:N0} gil");
            }

            if (ImGui.Button("Cancel Verification"))
                Apply(plugin.Sessions.CancelTradeVerification());

            ImGui.SameLine();
            if (ImGui.Button("Record Expected Amount Manually"))
            {
                Apply(plugin.Sessions.RecordTradeManually(
                    pendingTrade.PlayerName,
                    pendingTrade.ExpectedAmount));
            }

            return;
        }

        var round = plugin.Sessions.ActiveRound;
        if (round == null)
        {
            ImGui.Text("New participant");
            ImGui.SetNextItemWidth(260);
            ImGui.InputText("Player name", ref participantName, 64);
            ImGui.SameLine();
            if (ImGui.Button("Use Target"))
            {
                if (plugin.TryGetTargetedPlayerName(out var targetedPlayer))
                {
                    participantName = targetedPlayer;
                    SetStatus($"Selected {targetedPlayer}.", false);
                }
                else
                {
                    SetStatus("Target a player character first.", true);
                }
            }
            ImGui.SetNextItemWidth(180);
            ImGui.InputInt("Traded gil", ref tradeAmount, plugin.Configuration.ShotPrice);

            var previewRolls = plugin.Configuration.ShotPrice > 0
                ? Math.Max(0, tradeAmount / plugin.Configuration.ShotPrice)
                : 0;
            ImGui.TextDisabled(
                $"{previewRolls} roll(s) at {plugin.Configuration.ShotPrice:N0} gil each. " +
                "The amount must be an exact multiple.");

            if (ImGui.Button("Wait for Matching Trade"))
                Apply(plugin.Sessions.ArmTradeVerification(participantName, tradeAmount));

            ImGui.SameLine();
            if (ImGui.Button("Record Manually"))
                Apply(plugin.Sessions.RecordTradeManually(participantName, tradeAmount));

            return;
        }

        ImGui.Text($"{round.PlayerName}");
        ImGui.SameLine();
        ImGui.TextDisabled(
            $"Paid {round.PaidGil:N0} | Purchased {round.PurchasedRolls} | Remaining {round.RemainingRolls} | " +
            $"Payout {round.TotalPayout:N0}");

        participantName = round.PlayerName;
        ImGui.SetNextItemWidth(180);
        ImGui.InputInt("Additional traded gil", ref tradeAmount, plugin.Configuration.ShotPrice);
        if (ImGui.Button("Wait for Additional Trade"))
            Apply(plugin.Sessions.ArmTradeVerification(round.PlayerName, tradeAmount));

        ImGui.SameLine();
        if (ImGui.Button("Add Manually"))
            Apply(plugin.Sessions.RecordTradeManually(round.PlayerName, tradeAmount));

        ImGui.SameLine();
        if (ImGui.Button("End Player"))
            Apply(plugin.Sessions.FinishActiveRound());

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt("Manual roll", ref manualRoll);
        ImGui.SameLine();
        if (ImGui.Button("Record Manual"))
            Apply(plugin.Sessions.RecordRoll(round.PlayerName, manualRoll, true));

        ImGui.TextDisabled(
            "Listening for /random messages from this participant. Manual entry is available for corrections.");
    }

    private void DrawRolls(NightSession session)
    {
        var round = plugin.Sessions.ActiveRound ?? session.Rounds.LastOrDefault();
        if (round == null)
        {
            ImGui.TextDisabled("No rolls recorded tonight.");
            return;
        }

        ImGui.Text($"Roll history: {round.PlayerName}");
        using var child = ImRaii.Child("RollHistory", new Vector2(0, 155), true);
        if (!child.Success)
            return;

        if (!ImGui.BeginTable(
                "RollTable",
                5,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, 38);
        ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 65);
        ImGui.TableSetupColumn("Outcome");
        ImGui.TableSetupColumn("Payout", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableHeadersRow();

        foreach (var roll in round.Rolls.AsEnumerable().Reverse())
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(roll.Counter.ToString());
            ImGui.TableNextColumn();
            ImGui.Text(roll.Value.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roll.Outcome);
            ImGui.TableNextColumn();
            ImGui.Text($"{roll.Payout:N0}");
            ImGui.TableNextColumn();
            ImGui.TextDisabled(roll.WasManual ? "Manual" : "Chat");
        }

        ImGui.EndTable();
    }

    private static void DrawSales(NightSession session)
    {
        ImGui.Text("Night ledger");
        using var child = ImRaii.Child("SaleHistory", new Vector2(0, 145), true);
        if (!child.Success)
            return;

        if (!ImGui.BeginTable(
                "SalesTable",
                7,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupColumn("Player");
        ImGui.TableSetupColumn("Trade");
        ImGui.TableSetupColumn("Rolls");
        ImGui.TableSetupColumn("Jackpot");
        ImGui.TableSetupColumn("House");
        ImGui.TableSetupColumn("Dealer");
        ImGui.TableSetupColumn("Verified");
        ImGui.TableHeadersRow();

        foreach (var sale in session.Sales.AsEnumerable().Reverse())
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(sale.PlayerName);
            ImGui.TableNextColumn();
            ImGui.Text($"{sale.Amount:N0}");
            ImGui.TableNextColumn();
            ImGui.Text(sale.RollsPurchased.ToString());
            ImGui.TableNextColumn();
            ImGui.Text($"{sale.JackpotContribution:N0}");
            ImGui.TableNextColumn();
            ImGui.Text($"{sale.HouseCut:N0}");
            ImGui.TableNextColumn();
            ImGui.Text($"{sale.DealerCut:N0}");
            ImGui.TableNextColumn();
            ImGui.TextColored(
                sale.WasVerified
                    ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
                    : new Vector4(1f, 0.78f, 0.25f, 1f),
                sale.WasVerified ? "Chat" : "Manual");
        }

        ImGui.EndTable();
    }

    private void DrawHistory()
    {
        if (plugin.Configuration.SessionHistory.Count == 0)
            return;

        ImGui.Spacing();
        ImGui.Text("Recent nights");
        foreach (var session in plugin.Configuration.SessionHistory.Take(10))
        {
            var ended = session.EndedAt?.ToLocalTime().ToString("g") ?? "Open";
            ImGui.BulletText(
                $"{ended}: intake {session.TotalIntake:N0}, payouts {session.TotalPayouts:N0}, " +
                $"house {session.HouseCut:N0}, dealer {session.DealerCut:N0}, jackpot {session.EndingJackpot:N0}");
        }
    }

    private static void Metric(string label, long value)
    {
        ImGui.TextDisabled(label);
        ImGui.Text($"{value:N0} gil");
    }

    private void Apply(OperationResult result)
    {
        SetStatus(result.Message, !result.Success);
        if (result.Success)
            tradeAmount = 0;
    }
}
