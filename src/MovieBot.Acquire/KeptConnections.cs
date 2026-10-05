using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace TheKrystalShip.MovieBot.Acquire;

/// <summary>
/// The connections a client asked while somebody waits keeps open between requests.
///
/// A request on an open connection to the title index or the film database is answered in about
/// 15 ms; one that has to open it first pays DNS, TCP and TLS, about 100 ms more. So the client keeps
/// one handler for the life of the process rather than the factory's rotating ones, and speaks HTTP/2
/// where the server does, one connection carrying every request of a search at once. Both servers
/// close a connection left idle somewhere between two and four minutes; an HTTP/2 ping every
/// <see cref="PingEvery"/> keeps it open, at the cost of a frame and no request, until it has been
/// idle for <see cref="IdleKept"/>. A connection is still replaced after <see cref="Lifetime"/>, so a
/// change of the server's address is picked up.
/// </summary>
public static class KeptConnections
{
    internal static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan IdleKept = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public static IHttpClientBuilder KeepConnectionsOpen(this IHttpClientBuilder builder) => builder
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = IdleKept,
            PooledConnectionLifetime = Lifetime,
            KeepAlivePingDelay = PingEvery,
            KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
            EnableMultipleHttp2Connections = true,
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .ConfigureHttpClient(http =>
        {
            http.DefaultRequestVersion = HttpVersion.Version20;
            http.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        });
}
