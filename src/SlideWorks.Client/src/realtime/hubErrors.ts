// DeckHub.DECK_NOT_FOUND (src/SlideWorks.WebApi/Realtime/DeckHub.cs): the HubException message a refused join carries
// (JoinDeck, JoinPresentation). The SignalR client reports it as an error whose message ends with
// "HubException: Deck not found". No imports, so the component-test hub stub can share it.
export const DECK_NOT_FOUND = 'Deck not found';
