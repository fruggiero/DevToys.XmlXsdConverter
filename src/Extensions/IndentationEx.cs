using DevToys.XmlXsd.Models;

namespace DevToys.XmlXsd.Extensions;

internal static class IndentationEx
{
    public static string ToIndentChars(this Indentation indentation)
    {
        return indentation switch
        {
            Indentation.TwoSpaces => "  ",
            Indentation.FourSpaces => "    ",
            Indentation.OneTab => "\t",
            _ => throw new ArgumentException()
        };
    }
}