using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShotTracker.Models;

namespace ShotTracker.Services;

public sealed class CsvSyncService
{
    private const string FormatVersion = "1";

    private static readonly string[] Columns =
    [
        "RecordType",
        "SessionId",
        "RoundId",
        "RecordId",
        "StartedAt",
        "EndedAt",
        "PlayerName",
        "StartingJackpot",
        "EndingJackpot",
        "ActiveRoundId",
        "Timestamp",
        "Amount",
        "RollsPurchased",
        "JackpotContribution",
        "HouseCut",
        "DealerCut",
        "UnallocatedReserve",
        "WasVerified",
        "PaidGil",
        "PurchasedRolls",
        "RemainingRolls",
        "TotalPayout",
        "ExternalPrizesWon",
        "Counter",
        "RollValue",
        "WasManual",
        "GrantedReroll",
        "Payout",
        "JackpotPayout",
        "ExternalPrizes",
        "IsWin",
        "HighlightWin",
        "Outcome",
        "TotalIntake",
        "JackpotContributions",
        "TotalPayouts",
        "ExternalPrizesAwarded",
        "ShotPrice",
        "JackpotPercent",
        "HousePercent",
        "DealerPercent",
        "Label",
        "WinningNumber",
        "RangeEnd",
        "PayoutKind",
        "FixedPayoutGil",
        "FixedPayoutFromJackpot",
        "JackpotPayoutPercent",
        "ExternalPrize",
        "HighlightWinningRoll",
        "SendEcho",
        "EchoMessage",
        "ChatMessage",
        "ChatChannels",
        "Enabled",
    ];

    private readonly Configuration configuration;

    public CsvSyncService(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public OperationResult Export(string path)
    {
        try
        {
            path = NormalizePath(path);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            WriteRow(writer, Columns);
            WriteValues(writer, new()
            {
                ["RecordType"] = "Metadata",
                ["RecordId"] = FormatVersion,
                ["Amount"] = Format(configuration.JackpotBalance),
                ["Timestamp"] = Format(DateTimeOffset.Now),
            });
            WriteValues(writer, new()
            {
                ["RecordType"] = "Configuration",
                ["ShotPrice"] = Format(configuration.ShotPrice),
                ["JackpotPercent"] = Format(configuration.JackpotPercent),
                ["HousePercent"] = Format(configuration.HousePercent),
                ["DealerPercent"] = Format(configuration.DealerPercent),
            });
            foreach (var rule in configuration.WinRules)
            {
                WriteValues(writer, new()
                {
                    ["RecordType"] = "WinRule",
                    ["RecordId"] = Format(rule.Id),
                    ["Label"] = rule.Label,
                    ["WinningNumber"] = Format(rule.Number),
                    ["RangeEnd"] = Format(rule.RangeEnd),
                    ["PayoutKind"] = rule.PayoutKind.ToString(),
                    ["FixedPayoutGil"] = Format(rule.FixedPayoutGil),
                    ["FixedPayoutFromJackpot"] = Format(rule.FixedPayoutFromJackpot),
                    ["JackpotPayoutPercent"] = Format(rule.JackpotPayoutPercent),
                    ["ExternalPrize"] = rule.ExternalPrize,
                    ["GrantedReroll"] = Format(rule.GrantsReroll),
                    ["HighlightWinningRoll"] = Format(rule.HighlightWinningRoll),
                    ["SendEcho"] = Format(rule.SendEcho),
                    ["EchoMessage"] = rule.EchoMessage,
                    ["ChatMessage"] = rule.ChatMessage,
                    ["ChatChannels"] = string.Join(";", rule.ChatChannels ?? []),
                    ["Enabled"] = Format(rule.Enabled),
                });
            }

            var sessions = GetAllSessions()
                .OrderBy(session => session.StartedAt)
                .ToList();
            foreach (var session in sessions)
                WriteSession(writer, session);

            return OperationResult.Ok(
                $"Exported {sessions.Count} night(s) to {path}.");
        }
        catch (Exception exception)
        {
            return OperationResult.Fail($"CSV export failed: {exception.Message}");
        }
    }

    public OperationResult Import(string path)
    {
        try
        {
            path = NormalizePath(path);
            var document = ReadDocument(path);
            var importedActive = document.Sessions.SingleOrDefault(session => session.EndedAt == null);
            if (importedActive != null &&
                configuration.ActiveSession is { } localActive &&
                localActive.Id != importedActive.Id)
            {
                return OperationResult.Fail(
                    "The CSV contains a different active night. Close one night or sync from the same starting export.");
            }

            if (document.Settings != null)
            {
                configuration.ShotPrice = document.Settings.ShotPrice;
                configuration.JackpotPercent = document.Settings.JackpotPercent;
                configuration.HousePercent = document.Settings.HousePercent;
                configuration.DealerPercent = document.Settings.DealerPercent;
                configuration.WinRules = document.Settings.WinRules;
            }

            var sessionsAdded = 0;
            var recordsAdded = 0;
            foreach (var imported in document.Sessions)
            {
                var existing = FindSession(imported.Id);
                if (existing == null)
                {
                    NormalizeSession(imported);
                    AddSession(imported);
                    sessionsAdded++;
                    recordsAdded += imported.Sales.Count +
                                    imported.Rounds.Sum(round => round.Rolls.Count);
                    continue;
                }

                recordsAdded += MergeSession(existing, imported);
            }

            configuration.SessionHistory = configuration.SessionHistory
                .Where(session => configuration.ActiveSession?.Id != session.Id)
                .GroupBy(session => session.Id)
                .Select(group => group.First())
                .OrderByDescending(session => session.EndedAt ?? session.StartedAt)
                .ToList();

            if (configuration.ActiveSession != null)
            {
                configuration.JackpotBalance = configuration.ActiveSession.EndingJackpot;
            }
            else
            {
                var latestLocal = configuration.SessionHistory.FirstOrDefault();
                var latestImported = document.Sessions
                    .Where(session => session.EndedAt != null)
                    .OrderByDescending(session => session.EndedAt)
                    .FirstOrDefault();
                if (latestImported != null && latestLocal?.Id == latestImported.Id)
                    configuration.JackpotBalance = latestLocal.EndingJackpot;
                else if (configuration.SessionHistory.Count == 0 && document.CurrentJackpot is { } jackpot)
                    configuration.JackpotBalance = jackpot;
            }

            configuration.PendingTrade = null;
            configuration.Save();
            return OperationResult.Ok(
                $"Imported CSV: {sessionsAdded} night(s) and {recordsAdded} new sale/roll record(s).");
        }
        catch (Exception exception)
        {
            return OperationResult.Fail($"CSV import failed: {exception.Message}");
        }
    }

    public static string GetDefaultExportPath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ShotTracker");
        return Path.Combine(folder, $"ShotTracker-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv");
    }

    private IEnumerable<NightSession> GetAllSessions()
    {
        if (configuration.ActiveSession != null)
            yield return configuration.ActiveSession;

        foreach (var session in configuration.SessionHistory)
        {
            if (configuration.ActiveSession?.Id != session.Id)
                yield return session;
        }
    }

    private void AddSession(NightSession session)
    {
        if (session.EndedAt == null)
            configuration.ActiveSession = session;
        else
            configuration.SessionHistory.Add(session);
    }

    private NightSession? FindSession(Guid id)
    {
        if (configuration.ActiveSession?.Id == id)
            return configuration.ActiveSession;

        return configuration.SessionHistory.FirstOrDefault(session => session.Id == id);
    }

    private static int MergeSession(NightSession existing, NightSession imported)
    {
        var added = 0;
        existing.StartedAt = existing.StartedAt <= imported.StartedAt
            ? existing.StartedAt
            : imported.StartedAt;
        existing.EndedAt = Latest(existing.EndedAt, imported.EndedAt);
        if (existing.StartingJackpot == 0 && imported.StartingJackpot != 0)
            existing.StartingJackpot = imported.StartingJackpot;

        foreach (var importedRound in imported.Rounds)
        {
            var round = existing.Rounds.FirstOrDefault(item => item.Id == importedRound.Id);
            if (round == null)
            {
                existing.Rounds.Add(importedRound);
                added += importedRound.Rolls.Count;
                continue;
            }

            round.StartedAt = round.StartedAt <= importedRound.StartedAt
                ? round.StartedAt
                : importedRound.StartedAt;
            round.EndedAt = Latest(round.EndedAt, importedRound.EndedAt);
            if (round.PlayerName.Length == 0)
                round.PlayerName = importedRound.PlayerName;

            var rollIds = round.Rolls.Select(roll => roll.Id).ToHashSet();
            foreach (var roll in importedRound.Rolls.Where(roll => rollIds.Add(roll.Id)))
            {
                round.Rolls.Add(roll);
                added++;
            }
        }

        var saleIds = existing.Sales.Select(sale => sale.Id).ToHashSet();
        foreach (var sale in imported.Sales.Where(sale => saleIds.Add(sale.Id)))
        {
            existing.Sales.Add(sale);
            added++;
        }

        NormalizeSession(existing);
        return added;
    }

    private static void NormalizeSession(NightSession session)
    {
        session.Rounds = session.Rounds
            .GroupBy(round => round.Id)
            .Select(group => group.First())
            .OrderBy(round => round.StartedAt)
            .ToList();
        session.Sales = session.Sales
            .GroupBy(sale => sale.Id)
            .Select(group => group.First())
            .OrderBy(sale => sale.Timestamp)
            .ToList();

        foreach (var round in session.Rounds)
        {
            round.Rolls = round.Rolls
                .GroupBy(roll => roll.Id)
                .Select(group => group.First())
                .OrderBy(roll => roll.Timestamp)
                .ThenBy(roll => roll.Counter)
                .ToList();

            for (var index = 0; index < round.Rolls.Count; index++)
                round.Rolls[index].Counter = index + 1;

            var sales = session.Sales.Where(sale => sale.RoundId == round.Id).ToList();
            round.PaidGil = sales.Sum(sale => sale.Amount);
            round.PurchasedRolls = sales.Sum(sale => sale.RollsPurchased);
            round.TotalPayout = round.Rolls.Sum(roll => roll.Payout);
            round.ExternalPrizesWon = round.Rolls.Sum(roll => roll.ExternalPrizes.Count);
            var consumedRolls = round.Rolls.Count(roll => !roll.GrantedReroll);
            round.RemainingRolls = Math.Max(0, round.PurchasedRolls - consumedRolls);
        }

        session.TotalIntake = session.Sales.Sum(sale => sale.Amount);
        session.JackpotContributions = session.Sales.Sum(sale => sale.JackpotContribution);
        session.HouseCut = session.Sales.Sum(sale => sale.HouseCut);
        session.DealerCut = session.Sales.Sum(sale => sale.DealerCut);
        session.UnallocatedReserve = session.Sales.Sum(sale => sale.UnallocatedReserve);
        session.TotalPayouts = session.Rounds.Sum(round => round.TotalPayout);
        session.ExternalPrizesAwarded = session.Rounds.Sum(round => round.ExternalPrizesWon);
        var jackpotPayouts = session.Rounds.Sum(round => round.Rolls.Sum(roll => roll.JackpotPayout));
        session.EndingJackpot = Math.Max(0, session.StartingJackpot + session.JackpotContributions - jackpotPayouts);

        if (session.ActiveRoundId is { } activeRoundId &&
            session.Rounds.All(round => round.Id != activeRoundId))
        {
            session.ActiveRoundId = null;
        }
    }

    private static DateTimeOffset? Latest(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left == null)
            return right;
        if (right == null)
            return left;
        return left >= right ? left : right;
    }

    private static CsvDocument ReadDocument(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true);
        var rows = ParseRows(reader).ToList();
        if (rows.Count == 0)
            throw new InvalidDataException("The file is empty.");

        var header = rows[0];
        var indexes = header
            .Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "RecordType", "SessionId", "RoundId", "RecordId" })
        {
            if (!indexes.ContainsKey(required))
                throw new InvalidDataException($"Missing required column '{required}'.");
        }

        var sessions = new Dictionary<Guid, NightSession>();
        long? currentJackpot = null;
        ImportedSettings? settings = null;
        foreach (var row in rows.Skip(1))
        {
            var recordType = Value(row, indexes, "RecordType");
            if (recordType.Length == 0)
                continue;

            if (recordType.Equals("Metadata", StringComparison.OrdinalIgnoreCase))
            {
                var version = Value(row, indexes, "RecordId");
                if (version != FormatVersion)
                    throw new InvalidDataException($"Unsupported ShotTracker CSV version '{version}'.");
                currentJackpot = ParseLong(row, indexes, "Amount");
                continue;
            }

            if (recordType.Equals("Configuration", StringComparison.OrdinalIgnoreCase))
            {
                settings ??= new ImportedSettings();
                settings.ShotPrice = ParseInt(row, indexes, "ShotPrice");
                settings.JackpotPercent = ParseFloat(row, indexes, "JackpotPercent");
                settings.HousePercent = ParseFloat(row, indexes, "HousePercent");
                settings.DealerPercent = ParseFloat(row, indexes, "DealerPercent");
                continue;
            }

            if (recordType.Equals("WinRule", StringComparison.OrdinalIgnoreCase))
            {
                settings ??= new ImportedSettings();
                settings.WinRules.Add(new WinRule
                {
                    Id = ParseGuid(row, indexes, "RecordId"),
                    Label = Value(row, indexes, "Label"),
                    Number = ParseInt(row, indexes, "WinningNumber"),
                    RangeEnd = ParseNullableInt(row, indexes, "RangeEnd"),
                    PayoutKind = Enum.TryParse<PayoutKind>(
                        Value(row, indexes, "PayoutKind"),
                        true,
                        out var payoutKind)
                        ? payoutKind
                        : PayoutKind.FixedGil,
                    FixedPayoutGil = ParseLong(row, indexes, "FixedPayoutGil"),
                    FixedPayoutFromJackpot = ParseBool(
                        row,
                        indexes,
                        "FixedPayoutFromJackpot",
                        defaultValue: true),
                    JackpotPayoutPercent = ParseFloat(row, indexes, "JackpotPayoutPercent"),
                    ExternalPrize = Value(row, indexes, "ExternalPrize"),
                    GrantsReroll = ParseBool(row, indexes, "GrantedReroll"),
                    HighlightWinningRoll = ParseBool(
                        row,
                        indexes,
                        "HighlightWinningRoll",
                        defaultValue: true),
                    SendEcho = ParseBool(row, indexes, "SendEcho"),
                    EchoMessage = ValueOrDefault(
                        row,
                        indexes,
                        "EchoMessage",
                        "WIN: {player} rolled {roll} ({rule}) - {award}"),
                    ChatMessage = ValueOrDefault(
                        row,
                        indexes,
                        "ChatMessage",
                        "Congratulations {player}! You rolled {roll} and won {award}!"),
                    ChatChannels = ParseChatChannels(row, indexes),
                    Enabled = ParseBool(row, indexes, "Enabled"),
                });
                continue;
            }

            var sessionId = ParseGuid(row, indexes, "SessionId");
            if (!sessions.TryGetValue(sessionId, out var session))
            {
                session = new NightSession { Id = sessionId };
                sessions.Add(sessionId, session);
            }

            switch (recordType.ToLowerInvariant())
            {
                case "session":
                    session.StartedAt = ParseDate(row, indexes, "StartedAt");
                    session.EndedAt = ParseNullableDate(row, indexes, "EndedAt");
                    session.StartingJackpot = ParseLong(row, indexes, "StartingJackpot");
                    session.EndingJackpot = ParseLong(row, indexes, "EndingJackpot");
                    session.ActiveRoundId = ParseNullableGuid(row, indexes, "ActiveRoundId");
                    break;
                case "round":
                    session.Rounds.Add(new PlayerRound
                    {
                        Id = ParseGuid(row, indexes, "RoundId"),
                        PlayerName = Value(row, indexes, "PlayerName"),
                        StartedAt = ParseDate(row, indexes, "StartedAt"),
                        EndedAt = ParseNullableDate(row, indexes, "EndedAt"),
                        PaidGil = ParseLong(row, indexes, "PaidGil"),
                        PurchasedRolls = ParseInt(row, indexes, "PurchasedRolls"),
                        RemainingRolls = ParseInt(row, indexes, "RemainingRolls"),
                        TotalPayout = ParseLong(row, indexes, "TotalPayout"),
                        ExternalPrizesWon = ParseInt(row, indexes, "ExternalPrizesWon"),
                    });
                    break;
                case "sale":
                    session.Sales.Add(new SaleRecord
                    {
                        Id = ParseGuid(row, indexes, "RecordId"),
                        RoundId = ParseGuid(row, indexes, "RoundId"),
                        Timestamp = ParseDate(row, indexes, "Timestamp"),
                        PlayerName = Value(row, indexes, "PlayerName"),
                        Amount = ParseLong(row, indexes, "Amount"),
                        RollsPurchased = ParseInt(row, indexes, "RollsPurchased"),
                        JackpotContribution = ParseLong(row, indexes, "JackpotContribution"),
                        HouseCut = ParseLong(row, indexes, "HouseCut"),
                        DealerCut = ParseLong(row, indexes, "DealerCut"),
                        UnallocatedReserve = ParseLong(row, indexes, "UnallocatedReserve"),
                        WasVerified = ParseBool(row, indexes, "WasVerified"),
                    });
                    break;
                case "roll":
                    var roundId = ParseGuid(row, indexes, "RoundId");
                    var round = session.Rounds.FirstOrDefault(item => item.Id == roundId);
                    if (round == null)
                    {
                        round = new PlayerRound { Id = roundId };
                        session.Rounds.Add(round);
                    }

                    var outcome = Value(row, indexes, "Outcome");
                    var isWin = ParseBool(
                        row,
                        indexes,
                        "IsWin",
                        defaultValue: !outcome.Equals("No win", StringComparison.OrdinalIgnoreCase));
                    round.Rolls.Add(new RollRecord
                    {
                        Id = ParseGuid(row, indexes, "RecordId"),
                        Timestamp = ParseDate(row, indexes, "Timestamp"),
                        Counter = ParseInt(row, indexes, "Counter"),
                        Value = ParseInt(row, indexes, "RollValue"),
                        WasManual = ParseBool(row, indexes, "WasManual"),
                        GrantedReroll = ParseBool(row, indexes, "GrantedReroll"),
                        Payout = ParseLong(row, indexes, "Payout"),
                        JackpotPayout = ParseLongOrDefault(
                            row,
                            indexes,
                            "JackpotPayout",
                            ParseLong(row, indexes, "Payout")),
                        ExternalPrizes = ParseList(row, indexes, "ExternalPrizes"),
                        IsWin = isWin,
                        HighlightWin = ParseBool(
                            row,
                            indexes,
                            "HighlightWin",
                            defaultValue: isWin),
                        Outcome = outcome,
                    });
                    break;
                default:
                    throw new InvalidDataException($"Unknown record type '{recordType}'.");
            }
        }

        foreach (var session in sessions.Values)
            NormalizeSession(session);
        ValidateSettings(settings);
        return new CsvDocument(sessions.Values.ToList(), currentJackpot, settings);
    }

    private static void ValidateSettings(ImportedSettings? settings)
    {
        if (settings == null)
            return;

        if (settings.ShotPrice <= 0)
            throw new InvalidDataException("ShotPrice must be greater than zero.");

        var percentages = new[]
        {
            settings.JackpotPercent,
            settings.HousePercent,
            settings.DealerPercent,
        };
        if (percentages.Any(value => !float.IsFinite(value) || value < 0) ||
            percentages.Sum() > 100)
        {
            throw new InvalidDataException(
                "Jackpot, house, and dealer percentages must be finite, non-negative, and total no more than 100.");
        }

        if (settings.WinRules.Any(rule =>
                rule.Number is < 0 or > 999 ||
                rule.RangeEnd is < 0 or > 999 ||
                rule.RangeEnd is { } rangeEnd && rangeEnd < rule.Number ||
                rule.FixedPayoutGil < 0 ||
                !float.IsFinite(rule.JackpotPayoutPercent) ||
                rule.JackpotPayoutPercent < 0))
        {
            throw new InvalidDataException("The CSV contains an invalid winning rule.");
        }
    }

    private static void WriteSession(TextWriter writer, NightSession session)
    {
        WriteValues(writer, new()
        {
            ["RecordType"] = "Session",
            ["SessionId"] = Format(session.Id),
            ["StartedAt"] = Format(session.StartedAt),
            ["EndedAt"] = Format(session.EndedAt),
            ["StartingJackpot"] = Format(session.StartingJackpot),
            ["EndingJackpot"] = Format(session.EndingJackpot),
            ["ActiveRoundId"] = Format(session.ActiveRoundId),
            ["TotalIntake"] = Format(session.TotalIntake),
            ["JackpotContributions"] = Format(session.JackpotContributions),
            ["HouseCut"] = Format(session.HouseCut),
            ["DealerCut"] = Format(session.DealerCut),
            ["UnallocatedReserve"] = Format(session.UnallocatedReserve),
            ["TotalPayouts"] = Format(session.TotalPayouts),
            ["ExternalPrizesAwarded"] = Format(session.ExternalPrizesAwarded),
        });

        foreach (var round in session.Rounds.OrderBy(item => item.StartedAt))
        {
            WriteValues(writer, new()
            {
                ["RecordType"] = "Round",
                ["SessionId"] = Format(session.Id),
                ["RoundId"] = Format(round.Id),
                ["StartedAt"] = Format(round.StartedAt),
                ["EndedAt"] = Format(round.EndedAt),
                ["PlayerName"] = round.PlayerName,
                ["PaidGil"] = Format(round.PaidGil),
                ["PurchasedRolls"] = Format(round.PurchasedRolls),
                ["RemainingRolls"] = Format(round.RemainingRolls),
                ["TotalPayout"] = Format(round.TotalPayout),
                ["ExternalPrizesWon"] = Format(round.ExternalPrizesWon),
            });

            foreach (var roll in round.Rolls.OrderBy(item => item.Timestamp))
            {
                WriteValues(writer, new()
                {
                    ["RecordType"] = "Roll",
                    ["SessionId"] = Format(session.Id),
                    ["RoundId"] = Format(round.Id),
                    ["RecordId"] = Format(roll.Id),
                    ["Timestamp"] = Format(roll.Timestamp),
                    ["PlayerName"] = round.PlayerName,
                    ["Counter"] = Format(roll.Counter),
                    ["RollValue"] = Format(roll.Value),
                    ["WasManual"] = Format(roll.WasManual),
                    ["GrantedReroll"] = Format(roll.GrantedReroll),
                    ["Payout"] = Format(roll.Payout),
                    ["JackpotPayout"] = Format(roll.JackpotPayout),
                    ["ExternalPrizes"] = string.Join(";", roll.ExternalPrizes),
                    ["IsWin"] = Format(roll.IsWin),
                    ["HighlightWin"] = Format(roll.HighlightWin),
                    ["Outcome"] = roll.Outcome,
                });
            }
        }

        foreach (var sale in session.Sales.OrderBy(item => item.Timestamp))
        {
            WriteValues(writer, new()
            {
                ["RecordType"] = "Sale",
                ["SessionId"] = Format(session.Id),
                ["RoundId"] = Format(sale.RoundId),
                ["RecordId"] = Format(sale.Id),
                ["Timestamp"] = Format(sale.Timestamp),
                ["PlayerName"] = sale.PlayerName,
                ["Amount"] = Format(sale.Amount),
                ["RollsPurchased"] = Format(sale.RollsPurchased),
                ["JackpotContribution"] = Format(sale.JackpotContribution),
                ["HouseCut"] = Format(sale.HouseCut),
                ["DealerCut"] = Format(sale.DealerCut),
                ["UnallocatedReserve"] = Format(sale.UnallocatedReserve),
                ["WasVerified"] = Format(sale.WasVerified),
            });
        }
    }

    private static IEnumerable<List<string>> ParseRows(TextReader reader)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        while (reader.Read() is var value && value >= 0)
        {
            var character = (char)value;
            if (quoted)
            {
                if (character == '"')
                {
                    if (reader.Peek() == '"')
                    {
                        reader.Read();
                        field.Append('"');
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    if (reader.Peek() == '\n')
                        reader.Read();
                    row.Add(field.ToString());
                    field.Clear();
                    yield return row;
                    row = [];
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    yield return row;
                    row = [];
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (quoted)
            throw new InvalidDataException("CSV ended inside a quoted field.");
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            yield return row;
        }
    }

    private static void WriteValues(TextWriter writer, Dictionary<string, string> values)
    {
        WriteRow(writer, Columns.Select(column => values.GetValueOrDefault(column, string.Empty)));
    }

    private static void WriteRow(TextWriter writer, IEnumerable<string> values)
    {
        writer.WriteLine(string.Join(",", values.Select(Escape)));
    }

    private static string Escape(string value)
    {
        if (!value.ContainsAny([',', '"', '\r', '\n']))
            return value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static string NormalizePath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (path.Length == 0)
            throw new InvalidDataException("Enter a CSV file path.");
        if (!Path.HasExtension(path))
            path += ".csv";
        return Path.GetFullPath(path);
    }

    private static string Value(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return indexes.TryGetValue(column, out var index) && index < row.Count
            ? row[index]
            : string.Empty;
    }

    private static string ValueOrDefault(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column,
        string defaultValue)
    {
        var value = Value(row, indexes, column);
        return value.Length == 0 ? defaultValue : value;
    }

    private static Guid ParseGuid(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return Guid.TryParse(Value(row, indexes, column), out var value)
            ? value
            : throw new InvalidDataException($"Invalid {column}.");
    }

    private static Guid? ParseNullableGuid(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        var text = Value(row, indexes, column);
        return text.Length == 0 ? null : Guid.Parse(text);
    }

    private static DateTimeOffset ParseDate(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return DateTimeOffset.TryParseExact(
            Value(row, indexes, column),
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var value)
            ? value
            : throw new InvalidDataException($"Invalid {column}.");
    }

    private static DateTimeOffset? ParseNullableDate(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        var text = Value(row, indexes, column);
        return text.Length == 0
            ? null
            : DateTimeOffset.ParseExact(
                text,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
    }

    private static long ParseLong(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return long.TryParse(Value(row, indexes, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static long ParseLongOrDefault(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column,
        long defaultValue)
    {
        var text = Value(row, indexes, column);
        return text.Length == 0
            ? defaultValue
            : long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
    }

    private static int ParseInt(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return int.TryParse(Value(row, indexes, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static int? ParseNullableInt(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        var text = Value(row, indexes, column);
        return text.Length == 0
            ? null
            : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new InvalidDataException($"Invalid {column}.");
    }

    private static bool ParseBool(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column,
        bool defaultValue = false)
    {
        var text = Value(row, indexes, column);
        return text.Length == 0
            ? defaultValue
            : bool.TryParse(text, out var value) && value;
    }

    private static List<string> ParseList(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return Value(row, indexes, column)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static List<WinChatChannel> ParseChatChannels(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes)
    {
        return ParseList(row, indexes, "ChatChannels")
            .Select(value => Enum.TryParse<WinChatChannel>(value, true, out var channel)
                ? (WinChatChannel?)channel
                : null)
            .Where(channel => channel.HasValue)
            .Select(channel => channel!.Value)
            .Distinct()
            .ToList();
    }

    private static float ParseFloat(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> indexes,
        string column)
    {
        return float.TryParse(
            Value(row, indexes, column),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
    }

    private static string Format(Guid value) => value.ToString("D");
    private static string Format(Guid? value) => value?.ToString("D") ?? string.Empty;
    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static string Format(DateTimeOffset? value) =>
        value?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Format(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    private static string Format(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Format(bool value) => value ? "true" : "false";

    private sealed record CsvDocument(
        List<NightSession> Sessions,
        long? CurrentJackpot,
        ImportedSettings? Settings);

    private sealed class ImportedSettings
    {
        public int ShotPrice { get; set; }
        public float JackpotPercent { get; set; }
        public float HousePercent { get; set; }
        public float DealerPercent { get; set; }
        public List<WinRule> WinRules { get; set; } = [];
    }
}
