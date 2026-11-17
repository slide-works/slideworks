using System.Text.Json.Serialization;

namespace SlideWorks.WebApi.Realtime;

// Where an audience session is (J3). IAudienceSessions is the only place that decides it: live while a session is open,
// ended for two hours after the last one closed, otherwise lobby (for a deck that may have a lobby). On the wire it is
// "lobby", "live" or "ended".
[JsonConverter(typeof(JsonStringEnumConverter<AudiencePhase>))]
public enum AudiencePhase
{
    [JsonStringEnumMemberName("lobby")]
    Lobby,

    [JsonStringEnumMemberName("live")]
    Live,

    [JsonStringEnumMemberName("ended")]
    Ended,
}
