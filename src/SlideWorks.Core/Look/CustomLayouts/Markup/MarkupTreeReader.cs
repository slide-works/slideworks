using System.Text;
using System.Xml;

namespace SlideWorks.Core.Look.CustomLayouts.Markup;

// Reads the markup as strict XML (no namespaces, no DTD, no resolver) and builds the checked tree in
// one pass with an explicit stack. Depth and the node count are checked as each element is met, before
// anything is built for it, so nesting in the input never becomes recursion here. Whatever the reader
// does not recognise is refused, never repaired. Nothing of the input is kept in an error.
internal sealed class MarkupTreeReader(CustomLayoutAllow allow)
{
    private readonly HashSet<string> tags = [.. allow.Tags];
    private readonly HashSet<string> attributes = [.. allow.Attributes];

    public (LayoutTree? Tree, IReadOnlyList<CustomLayoutError> Errors) Read(string markup)
    {
        var errors = new List<CustomLayoutError>();
        if (markup.Length > CustomLayoutLimits.MAX_PART_BYTES || Encoding.UTF8.GetByteCount(markup) > CustomLayoutLimits.MAX_PART_BYTES)
        {
            return (null, [CustomLayoutError.Of("layout.html:1", CustomLayoutErrorCodes.MARKUP_TOO_LARGE)]);
        }

        var root = new List<TreeNode>();
        try
        {
            ControlReferences(markup, errors);
            Walk(markup, root, errors);
        }
        catch (Exception)
        {
            // Fail closed: anything the reader or this code cannot handle is a refusal, never an exception.
            Add(errors, 1, CustomLayoutErrorCodes.MARKUP_MALFORMED);
        }

        if (errors.Count == 0 && root.Count == 0)
        {
            Add(errors, 1, CustomLayoutErrorCodes.MARKUP_MALFORMED);
        }

        return errors.Count == 0 ? (new LayoutTree(LayoutTree.VERSION, null, root), errors) : (null, errors);
    }

    // Refuses a numeric reference to a control character (tab, line feed and carriage return too) from the
    // raw text, whatever the reader would decode it to. A reference the reader cannot read is its to refuse.
    private static void ControlReferences(string markup, List<CustomLayoutError> errors)
    {
        var line = 1;
        var counted = 0;
        for (var at = markup.IndexOf("&#", StringComparison.Ordinal); at >= 0; at = markup.IndexOf("&#", at + 2, StringComparison.Ordinal))
        {
            var end = markup.IndexOf(';', at);
            if (end < 0 || end - at > 12)
            {
                continue;
            }

            var hex = markup[at + 2] is 'x';
            var digits = markup.AsSpan(at + (hex ? 3 : 2), end - at - (hex ? 3 : 2));
            if (!int.TryParse(digits, hex ? System.Globalization.NumberStyles.AllowHexSpecifier : System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var code)
                || !(code is < 0x20 or (>= 0x7F and <= 0x9F)))
            {
                continue;
            }

            line += markup.AsSpan(counted, at - counted).Count('\n');
            counted = at;
            Add(errors, line, CustomLayoutErrorCodes.MARKUP_MALFORMED);
        }
    }

    private void Walk(string markup, List<TreeNode> root, List<CustomLayoutError> errors)
    {
        using var reader = new XmlTextReader(markup, XmlNodeType.Element, null)
        {
            Namespaces = false,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            WhitespaceHandling = WhitespaceHandling.All,
        };

        var open = new Stack<Open>();
        var nodes = 0;
        try
        {
            while (errors.Count < MarkupAttributes.MAX_ERRORS && reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Depth + 1 > CustomLayoutLimits.MAX_MARKUP_DEPTH)
                        {
                            Add(errors, reader.LineNumber, CustomLayoutErrorCodes.MARKUP_DEPTH);
                            return;
                        }

                        if (++nodes > CustomLayoutLimits.MAX_NODES)
                        {
                            Add(errors, reader.LineNumber, CustomLayoutErrorCodes.MARKUP_TOO_MANY_NODES);
                            return;
                        }

                        var element = Start(reader, open, errors);
                        if (reader.IsEmptyElement)
                        {
                            Finish(element, open, root);
                        }
                        else
                        {
                            open.Push(element);
                        }

                        break;
                    case XmlNodeType.EndElement:
                        Finish(open.Pop(), open, root);
                        break;
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    case XmlNodeType.Comment:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.ProcessingInstruction:
                    case XmlNodeType.XmlDeclaration:
                    case XmlNodeType.DocumentType:
                        Add(errors, reader.LineNumber, CustomLayoutErrorCodes.MARKUP_COMMENT);
                        break;
                    default:
                        // Text, and anything else the reader can report: markup holds no text.
                        Add(errors, reader.LineNumber, CustomLayoutErrorCodes.MARKUP_TEXT);
                        break;
                }
            }
        }
        catch (XmlException exception)
        {
            Add(errors, Math.Max(exception.LineNumber, 1), CustomLayoutErrorCodes.MARKUP_MALFORMED);
        }
    }

    private Open Start(XmlTextReader reader, Stack<Open> open, List<CustomLayoutError> errors)
    {
        var line = reader.LineNumber;
        var tag = reader.Name;
        var parent = open.Count > 0 ? open.Peek() : null;
        if (!tags.Contains(tag))
        {
            Add(errors, line, CustomLayoutErrorCodes.MARKUP_TAG, tag);
        }

        if (parent?.HeldSlot is not null)
        {
            Add(errors, line, CustomLayoutErrorCodes.MARKUP_BINDING);
        }

        var element = new Open(tag) { HeldEachDepth = parent?.HeldEachDepth ?? 0 };
        string? step = null;
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                var name = reader.Name;
                var value = reader.Value;
                var at = reader.LineNumber;
                if (!attributes.Contains(name))
                {
                    Add(errors, at, CustomLayoutErrorCodes.MARKUP_ATTRIBUTE, name);
                    continue;
                }

                switch (name)
                {
                    case MarkupAttributes.CLASS:
                        if (!BindingParser.TryClasses(value, out var classes) || classes.Count > CustomLayoutLimits.MAX_CLASSES_PER_ELEMENT)
                        {
                            Add(errors, at, CustomLayoutErrorCodes.MARKUP_CLASS);
                        }
                        else
                        {
                            element.HeldClasses = classes;
                        }

                        break;
                    case MarkupAttributes.SLOT:
                        element.HeldSlot = Slot(value, at, errors);
                        break;
                    case MarkupAttributes.EACH:
                        element.HeldEach = Slot(value, at, errors);
                        if (++element.HeldEachDepth > MarkupAttributes.MAX_EACH_DEPTH)
                        {
                            Add(errors, at, CustomLayoutErrorCodes.MARKUP_BINDING);
                        }

                        break;
                    case MarkupAttributes.IF:
                        element.HeldIf = Slot(value, at, errors);
                        break;
                    case MarkupAttributes.VAR:
                        if (!BindingParser.TryVars(value, out var vars))
                        {
                            Add(errors, at, CustomLayoutErrorCodes.MARKUP_BINDING);
                        }
                        else
                        {
                            element.HeldVars = vars;
                        }

                        break;
                    case MarkupAttributes.VARIANT:
                        if (!BindingParser.TryVariants(value, out var variants))
                        {
                            Add(errors, at, CustomLayoutErrorCodes.MARKUP_BINDING);
                        }
                        else
                        {
                            element.HeldVariants = variants;
                        }

                        break;
                    case MarkupAttributes.STEP:
                        step = value;
                        element.HeldStepLine = at;
                        break;
                }
            }
            while (reader.MoveToNextAttribute());
            reader.MoveToElement();
        }

        if (step is not null)
        {
            element.HeldStep = Step(step, element, errors);
        }

        return element;
    }

    private static string? Slot(string value, int line, List<CustomLayoutError> errors)
    {
        if (BindingParser.IsSlotName(value))
        {
            return value;
        }

        Add(errors, line, CustomLayoutErrorCodes.MARKUP_BINDING);
        return null;
    }

    private static int? Step(string value, Open element, List<CustomLayoutError> errors)
    {
        if (element.HeldEach is not null && value == MarkupAttributes.STEP_EACH)
        {
            return TreeNode.STEP_EACH;
        }

        var number = element.HeldEach is null ? BindingParser.Step(value) : null;
        if (number is null)
        {
            Add(errors, element.HeldStepLine, CustomLayoutErrorCodes.MARKUP_BINDING);
        }

        return number;
    }

    private static void Finish(Open element, Stack<Open> open, List<TreeNode> root)
    {
        var node = new TreeNode(
            element.HeldTag,
            element.HeldClasses,
            element.HeldSlot,
            element.HeldEach,
            element.HeldIf,
            element.HeldStep,
            element.HeldVars,
            element.HeldVariants,
            element.HeldChildren);
        (open.Count > 0 ? open.Peek().HeldChildren : root).Add(node);
    }

    private static void Add(List<CustomLayoutError> errors, int line, string code, string? name = null)
    {
        if (errors.Count >= MarkupAttributes.MAX_ERRORS)
        {
            return;
        }

        var path = $"layout.html:{Math.Max(line, 1)}";
        errors.Add(name is not null && BindingParser.IsNameable(name)
            ? CustomLayoutError.Of(path, code, name)
            : CustomLayoutError.Of(path, code));
    }

    private sealed class Open(string tag)
    {
        public string HeldTag { get; } = tag;
        public IReadOnlyList<string> HeldClasses { get; set; } = [];
        public string? HeldSlot { get; set; }
        public string? HeldEach { get; set; }
        public string? HeldIf { get; set; }
        public int? HeldStep { get; set; }
        public int HeldStepLine { get; set; }
        public int HeldEachDepth { get; set; }
        public IReadOnlyList<TreeVar> HeldVars { get; set; } = [];
        public IReadOnlyList<TreeVariant> HeldVariants { get; set; } = [];
        internal List<TreeNode> HeldChildren { get; } = [];
    }
}
