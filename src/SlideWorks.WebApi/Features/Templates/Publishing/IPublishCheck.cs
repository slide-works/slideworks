namespace SlideWorks.WebApi.Features.Templates.Publishing;

// One row of the publish checklist. A check is pure: it reads the facts and writes nothing.
public interface IPublishCheck
{
    CheckOutcome Run(PublishFacts facts);
}
