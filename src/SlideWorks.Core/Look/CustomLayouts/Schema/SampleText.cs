using System.Text;

namespace SlideWorks.Core.Look.CustomLayouts.Schema;

// Text of an exact length that wraps like prose: words of 4 to 9 letters with single spaces, and wide
// letters (m, w) in every third word. Pure: the same length always gives the same text.
internal static class SampleText
{
    private const string NARROW = "abcdefghijklnopqrstuvxyz";
    private const int MIN_WORD = 4;
    private const int MAX_WORD = 9;
    private const int SEPARATED = MAX_WORD + 1;

    public static string Of(int length)
    {
        if (length < MIN_WORD)
        {
            return Word(Math.Max(length, 1), 0);
        }

        var words = (length + SEPARATED) / SEPARATED;
        var letters = length - (words - 1);
        var text = new StringBuilder(length);
        for (var i = 0; i < words; i++)
        {
            var size = letters / words + (i >= words - letters % words ? 1 : 0);
            text.Append(i == 0 ? "" : " ").Append(Word(size, i));
        }

        return text.ToString();
    }

    private static char Wide(int position) => position / 2 % 2 == 0 ? 'm' : 'w';

    private static string Word(int size, int index)
    {
        var word = new StringBuilder(size);
        for (var position = 0; position < size; position++)
        {
            word.Append(index % 3 == 2 && position % 2 == 0 ? Wide(position) : NARROW[((index * 7) + (position * 5)) % NARROW.Length]);
        }

        return word.ToString();
    }
}
