using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.OpenResponses.Contracts;
using SlideWorks.WebApi.Features.Polls.Contracts;
using SlideWorks.WebApi.Features.Qa.Contracts;

namespace SlideWorks.WebApi.Realtime;

// Something a guest or the owner's presenter view is sent on the hub method "audienceEvent" (J4). The "type" discriminator
// is the wire shape, mirrored in src/contracts/screens/audience-events.json. DeckId routes the event to a group and is
// never written: a guest learns nothing about the deck from a frame. SignalR and MVC serialise by the runtime type, so each
// concrete record (not sealed, because System.Text.Json refuses polymorphism on a sealed type) repeats the polymorphism attributes for itself: the discriminator is written whether the value is
// declared as the base type, as its own type, or as a property such as GuestView.State.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AudienceState), "audienceState")]
[JsonDerivedType(typeof(AudienceEnded), "audienceEnded")]
[JsonDerivedType(typeof(JoinedCount), "joinedCount")]
[JsonDerivedType(typeof(ReadingIn), "readingIn")]
[JsonDerivedType(typeof(PollTally), "pollTally")]
[JsonDerivedType(typeof(PollControl), "pollControl")]
[JsonDerivedType(typeof(QuestionTally), "questionTally")]
[JsonDerivedType(typeof(QuestionControl), "questionControl")]
[JsonDerivedType(typeof(ReactionsBurst), "reactions")]
[JsonDerivedType(typeof(WordCloudTally), "wordCloud")]
[JsonDerivedType(typeof(OpenTextTally), "openText")]
public abstract record AudienceEvent([property: JsonIgnore] string DeckId);
