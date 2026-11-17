namespace SlideWorks.WebApi.Realtime.Presenting;

// P10: told about every accepted change to a presentation (join, update, timer, leave, talk length, start), after the
// group broadcast and outside the per-deck lock. A refused call notifies nobody, and an observer that throws is logged
// and skipped. Register one with an Agoda.IoC attribute for this interface; SW-4 registers none.
public interface IPresentationObserver
{
    Task OnChangedAsync(PresentationState state);
}
