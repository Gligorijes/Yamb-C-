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
        if (_pending.TryRemove(gameId, out var decisions))
        {
            double labeled = Math.Clamp(double.IsFinite(reward) ? reward : 0, -1, 1);
            foreach (var decision in decisions)
            {
                string key = BotExperienceKeys.EntryKey(decision.Kind, decision.StateKey, decision.MoveKey);
                _entries.AddOrUpdate(
                    key,
                    _ => new BotExperienceValue(1, labeled, DateTime.UtcNow),
                    (_, old) =>
                    {
                        int count = old.Count + 1;
                        return new BotExperienceValue(
                            count,
                            old.Mean + (labeled - old.Mean) / count,
                            DateTime.UtcNow);
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
                if (data?.Entries is null) return;
                foreach (var pair in data.Entries)
                {
                    if (pair.Value is null) continue;
                    _entries[pair.Key] = new BotExperienceValue(
                        Math.Max(0, pair.Value.Count),
                        double.IsFinite(pair.Value.Mean) ? pair.Value.Mean : 0,
                        pair.Value.UpdatedAtUtc);
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
        foreach (var entry in _entries
                     .OrderBy(pair => pair.Value.UpdatedAtUtc)
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
                    Entries = _entries.ToDictionary(
                        pair => pair.Key,
                        pair => (EntryData?)new EntryData
                        {
                            Count = pair.Value.Count,
                            Mean = pair.Value.Mean,
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
        public int Version { get; set; } = 2;

        public Dictionary<string, EntryData?> Entries { get; set; } = new();
    }

    private sealed class EntryData
    {
        public int Count { get; set; }

        public double Mean { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }
}
