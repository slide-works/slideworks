using System.Text.Json;

namespace SlideWorks.WebApi.Features.Templates.Views;

// GET /api/templates/{id}/layouts/{name}/review: what the owner looks at before approving a custom layout
// (src/contracts/screens/layout-review.json). Asset is the layout file under review. Sample and Maximum are slots: a
// typical slide and the fullest one the layout allows.
public sealed record LayoutReviewView(
    string Name,
    string Asset,
    bool Approved,
    string Surface,
    int Steps,
    LayoutReviewLayout Layout,
    JsonElement Sample,
    JsonElement Maximum,
    LayoutReviewSafeArea SafeArea,
    LayoutReviewContrast Contrast);
