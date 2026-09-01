using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.MovieBot.Acquire.Tracker;

/// <summary>
/// The serializer for the tracker's wire shapes, shipped beside them so the snake_case names
/// cannot drift apart from the types. Every root is registered here; a type reached only by
/// reflection would throw at runtime.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(IReadOnlyList<TrackerTorrent>))]
[JsonSerializable(typeof(TrackerError))]
public partial class TrackerJsonContext : JsonSerializerContext;

/// <summary>The API refused a call, or answered with something that is not a result set.</summary>
public sealed class TrackerException(string message, Exception? inner = null)
    : Exception(message, inner);
