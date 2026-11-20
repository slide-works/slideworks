using System.ComponentModel;

namespace SlideWorks.WebApi.Features.Images;

// One file of an upload_images call. Both are checked by ImageUploads with the rules upload_image applies.
public sealed record UploadImagesFile(
    [property: Description("The image's file name, such as story-map.png, up to 200 characters. Shown in the deck's change log only.")] string? Name,
    [property: Description("image/png, image/jpeg, image/webp or image/gif.")] string? ContentType);
