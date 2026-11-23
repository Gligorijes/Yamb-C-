using System.Collections.Concurrent;
using System.Text.Json;
using Yamb.Application.Interfaces;
using Yamb.Application.Models;
using Yamb.Application.Services;

namespace Yamb.Infrastructure.Learning;

public sealed class JsonBotExperienceStore : IBotExperienceStore
{
    private const int MaxEntries = 20000;
    private const int MaxPendingGames = 16;
    private const int MaxDecisionsPerGame = 400;

    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, BotExperienceValue> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<BotDecisionRecord>> _pending = new();
    private readonly object _sync = new();
    private bool _loaded;

    public JsonBotExperienceStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Yamb",
            "bot-experience.json");
    }

    public int GamesCompleted { get; private set; }

    public BotExperienceValue? Get(BotDecisionKind kind, string stateKey, string moveKey)
    {
        EnsureLoaded();
        return _entries.TryGetValue(BotExperienceKeys.EntryKey(kind, stateKey, moveKey), out var value)
            ? value
            : null;
    }

    public void RememberDecision(BotDecisionRecord decision)
    {
        while (_pending.Count >= MaxPendingGames && _pending.TryRemove(_pending.Keys.First(), out _))
        {
        }

        var queue = _pending.GetOrAdd(decision.GameId, _ => new ConcurrentQueue<BotDecisionRecord>());
        if (queue.Count < MaxDecisionsPerGame) queue.Enqueue(decision);
    }

    public async Task CompleteGameAsync(Guid gameId, double reward)
    {
        EnsureLoaded();
        GamesCompleted++;

        if (_pending.TryRemove(gameId, out var decisions))
        {
            double labeled = Math.Clamp(double.IsFinite(reward) ? reward : 0, -1, 1);
            DateTime now = DateTime.UtcNow;
            var grouped = decisions
                .GroupBy(
                    decision => BotExperienceKeys.EntryKey(decision.Kind, decision.StateKey, decision.MoveKey),
                    StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                int added = group.Count();
                _entries.AddOrUpdate(
                    group.Key,
                    _ => new BotExperienceValue(added, labeled, now, 1),
                    (_, old) =>
                    {
                        int count = old.Count + added;
                        double mean = count == 0 ? 0 : (old.Mean * old.Count + labeled * added) / count;
                        return new BotExperienceValue(count, mean, now, old.Games + 1);
                    });
            }
        }

        Prune();
        await Task.Run(Save);
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_sync)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(_filePath)) return;
                var data = JsonSerializer.Deserialize<FileData>(File.ReadAllText(_filePath));
                if (data?.Entries is null || data.Version < 3) return;

                GamesCompleted = Math.Max(0, data.GamesCompleted);
                foreach (var pair in data.Entries)
                {
                    if (pair.Value is null) continue;
                    _entries[pair.Key] = new BotExperienceValue(
                        Math.Max(0, pair.Value.Count),
                        double.IsFinite(pair.Value.Mean) ? pair.Value.Mean : 0,
                        pair.Value.UpdatedAtUtc,
                        Math.Max(0, pair.Value.Games ?? 0));
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
            }
        }
    }

    private void Prune()
    {
        if (_entries.Count <= MaxEntries) return;
        var cutoff = DateTime.UtcNow.AddDays(-45);
        foreach (var entry in _entries
                     .OrderBy(pair => pair.Value.Games + pair.Value.Count / 20.0)
                     .ThenBy(pair => pair.Value.UpdatedAtUtc < cutoff ? 0 : 1)
                     .ThenBy(pair => pair.Value.UpdatedAtUtc)
                     .Take(_entries.Count - MaxEntries))
        {
            _entries.TryRemove(entry.Key, out _);
        }
    }

    private void Save()
    {
        lock (_sync)
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var data = new FileData
                {
                    Version = 3,
                    GamesCompleted = GamesCompleted,
                    Entries = _entries.ToDictionary(
                        pair => pair.Key,
                        pair => (EntryData?)new EntryData
                        {
                            Count = pair.Value.Count,
                            Mean = pair.Value.Mean,
                            Games = pair.Value.Games,
                            UpdatedAtUtc = pair.Value.UpdatedAtUtc
                        })
                };

                string temporary = _filePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(data));
                File.Move(temporary, _filePath, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FileData
    {
        public int Version { get; set; } = 3;

        public int GamesCompleted { get; set; }

        public Dictionary<string, EntryData?> Entries { get; set; } = new();
    }

    private sealed class EntryData
    {
        public int Count { get; set; }

        public double Mean { get; set; }

        public int? Games { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
