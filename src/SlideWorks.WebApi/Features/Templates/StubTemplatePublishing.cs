using Agoda.IoC.Core;
using SlideWorks.WebApi.Features.Templates.Views;
using SlideWorks.WebApi.Infrastructure.Identity;

namespace SlideWorks.WebApi.Features.Templates;

// TODO: returns the screen sample until the real service lands.
[RegisterPerRequest(For = typeof(ITemplatePublishing))]
public sealed class StubTemplatePublishing : ITemplatePublishing
{
    // src/contracts/screens/template-editor.json: its checklist and canPublish
    private const string CHECKLIST =
        """
        {
          "checklist": [
            {
              "key": "palette",
              "label": "Palette passes contrast",
              "state": "pass",
              "detail": "Every pair meets its bar.",
              "tab": "palette"
            },
            {
              "key": "fonts",
              "label": "Fonts fit slide text",
              "state": "pass",
              "detail": "4 font files, all within 0.45 to 0.70 em.",
              "tab": "fonts"
            },
            {
              "key": "fontRights",
              "label": "Font rights confirmed",
              "state": "todo",
              "detail": "Confirm you can serve these fonts.",
              "tab": "fonts"
            },
            {
              "key": "logoArt",
              "label": "Logo and art are safe to serve",
              "state": "pass",
              "detail": "",
              "tab": "logo-art"
            },
            {
              "key": "limits",
              "label": "Within the limits",
              "state": "fail",
              "detail": "The purpose is longer than 200 characters.",
              "tab": "basics"
            }
          ],
          "canPublish": false
        }
        """;

    // src/contracts/screens/layout-review.json
    private const string REVIEW =
        """
        {
          "name": "stage-ladder",
          "asset": "a5c2b551af63022db28da61ae35baf09",
          "approved": false,
          "surface": "light",
          "steps": 1,
          "layout": {
            "name": "stage-ladder",
            "surface": "light",
            "tree": {
              "tag": "div",
              "class": "root",
              "children": []
            },
            "css": "",
            "slots": [
              {
                "name": "headline",
                "type": "text",
                "required": true,
                "max": 80
              }
            ]
          },
          "sample": {
            "headline": "Four stages, one ladder"
          },
          "maximum": {
            "headline": "MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM"
          },
          "safeArea": {
            "ok": false,
            "overflow": [
              "headline"
            ]
          },
          "contrast": {
            "ok": false,
            "failures": [
              "muted on chip"
            ]
          }
        }
        """;

    public Task<TemplateChecklistView> ChecklistAsync(ICurrentActor actor, string id) => Task.FromResult(TemplateViewJson.Read<TemplateChecklistView>(CHECKLIST));

    public Task<TemplatePublished> PublishAsync(ICurrentActor actor, string id) => Task.FromResult(new TemplatePublished(3, 2));

    public Task ArchiveAsync(ICurrentActor actor, string id) => Task.CompletedTask;

    public Task<LayoutReviewView> LayoutReviewAsync(ICurrentActor actor, string id, string name) => Task.FromResult(TemplateViewJson.Read<LayoutReviewView>(REVIEW));

    public Task ApproveLayoutAsync(ICurrentActor actor, string id, string name, string asset, IReadOnlyList<string> overflow, IReadOnlyList<string> contrast)
        => Task.CompletedTask;
}
