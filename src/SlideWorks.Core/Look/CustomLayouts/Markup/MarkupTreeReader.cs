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

    // One linear pass over the raw text, before the reader normalises anything. It refuses a numeric
    // reference to a control character (tab, line feed and carriage return too), however long its
    // zero padding, and any raw control character inside a quoted attribute value (the reader would turn
    // a raw tab, CR or LF into a space). Newlines between attributes or in text stay allowed. A reference
    // the reader cannot read is its to refuse; the numeric value is capped, so no input is ever parsed twice.
    private static void ControlReferences(string markup, List<CustomLayoutError> errors)
    {
        const int CAP = 0x110000;
        var line = 1;
        var inTag = false;
        var quote = '\0';
        var valueRefused = false;
        var lineBreak = false;
        string? skipUntil = null;
        var i = -1;
        while (++i < markup.Length)
        {
            // Line ends as the XML reader counts them: CRLF is one, a lone CR is one, LF is one. The count
            // moves on at the character after the break, so a character is reported on its own line.
            if (lineBreak)
            {
                line++;
            }

            var c = markup[i];
            lineBreak = c == '\n' || (c == '\r' && (i + 1 >= markup.Length || markup[i + 1] != '\n'));

            if (skipUntil is not null)
            {
                // Inside a comment, CDATA section or processing instruction: refused later as markup_comment.
                if (string.CompareOrdinal(markup, i, skipUntil, 0, skipUntil.Length) == 0)
                {
                    i += skipUntil.Length - 1;
                    skipUntil = null;
                }

                continue;
            }

            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                    continue;
                }

                if (c < 0x20 && !valueRefused)
                {
                    valueRefused = true;
                    Add(errors, line, CustomLayoutErrorCodes.MARKUP_MALFORMED);
                    continue;
                }
            }
            else if (inTag)
            {
                if (c == '>')
                {
                    inTag = false;
                }
                else if (c is '"' or '\'')
                {
                    quote = c;
                    valueRefused = false;
                }
            }
            else if (c == '<')
            {
                skipUntil = SkipTerminator(markup, i);
                inTag = skipUntil is null && i + 1 < markup.Length && markup[i + 1] is not ('!' or '?');
                if (skipUntil is not null)
                {
                    continue;
                }
            }

            if (c != '&' || i + 1 >= markup.Length || markup[i + 1] != '#')
            {
                continue;
            }

            var at = i + 2;
            var hex = at < markup.Length && markup[at] is 'x' or 'X';
            if (hex)
            {
                at++;
            }

            var code = 0;
            var digits = 0;
            for (; at < markup.Length; at++)
            {
                var value = DigitValue(markup[at], hex);
                if (value < 0)
                {
                    break;
                }

                digits++;
                code = Math.Min((code * (hex ? 16 : 10)) + value, CAP);
            }

            if (digits > 0 && at < markup.Length && markup[at] == ';' && (code < 0x20 || code is >= 0x7F and <= 0x9F))
            {
                Add(errors, line, CustomLayoutErrorCodes.MARKUP_MALFORMED);
            }

            i = at - 1;
        }
    }

    private static string? SkipTerminator(string markup, int at)
    {
        if (string.CompareOrdinal(markup, at, "<!--", 0, 4) == 0)
        {
            return "-->";
        }

        if (string.CompareOrdinal(markup, at, "<![CDATA[", 0, 9) == 0)
        {
            return "]]>";
        }

        return string.CompareOrdinal(markup, at, "<?", 0, 2) == 0 ? "?>" : null;
    }

    private static int DigitValue(char d, bool hex)
    {
        if (d is >= '0' and <= '9')
        {
            return d - '0';
        }

        if (!hex)
        {
            return -1;
        }

        return char.ToLowerInvariant(d) is >= 'a' and <= 'f' ? char.ToLowerInvariant(d) - 'a' + 10 : -1;
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
